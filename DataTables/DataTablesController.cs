using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Azure.Storage.Blobs;
using System.Text.Json;
using System.Threading.Tasks;
using Syncfusion.DocIO.DLS;
using System.Text;
using HarfBuzzSharp;

namespace CCRM2.Server.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class DataTablesController : ControllerBase
    {
        private readonly CCRM2DBContext _context;
        private readonly IConfiguration _configuration;
        private readonly SearchIndexerClient _indexerClient;
        private readonly SearchIndexClient _indexClient;
        private readonly string _blobConnectionString;
        private readonly string _containerName = "exported-data-tenant";
        private readonly string schemaName = "crm";


        public DataTablesController(CCRM2DBContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            string searchEndpoint = _configuration["AzureAISearch:Endpoint"];
            string searchIndexName = "azuresql-index-tenant-" + _configuration["Auth0:tenant_id"];
            string searchApiKey = _configuration["AzureAISearch:Key"];
            _containerName = _containerName + "-" + _configuration["Auth0:tenant_id"];
            _indexerClient = new SearchIndexerClient(new Uri(searchEndpoint), new AzureKeyCredential(searchApiKey));
            _indexClient = new SearchIndexClient(new Uri(searchEndpoint), new AzureKeyCredential(searchApiKey));
            _blobConnectionString = _configuration["BlobStorage:ConnectionString"];
        }
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CCRM2.Shared.Models.DataTable>>> GetTables()
        {
            return await _context.DataTables.ToListAsync();
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("refresh")]
        public async Task<ActionResult> PostTables(List<CCRM2.Shared.Models.DataTable> cards)
        {
            if (cards != null)
            {
                _context.DataTables.RemoveRange(_context.DataTables);
            }
            string connectionString = _configuration["ConnectionStrings:DefaultConnection"];

            string query = $"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schemaName}'";
            List<string> tablesFromServer = new List<string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string tableName = reader["TABLE_NAME"].ToString();
                    tablesFromServer.Add(tableName);
                }
                reader.Close();
            }
            int i = 1;
            tablesFromServer.Sort();
            List<CCRM2.Shared.Models.DataTable> tables = new List<CCRM2.Shared.Models.DataTable>();
            foreach (string s in tablesFromServer)
            {
                CCRM2.Shared.Models.DataTable table = new CCRM2.Shared.Models.DataTable { Id = i, Selected = false, Title = s };
                tables.Add(table);
                i++;
            }
            await _context.AddRangeAsync(tables);
            await _context.SaveChangesAsync();
            return Ok();
        }


        [Authorize(Roles = "Admin,Config")]
        [HttpPut("UpdateAll")]
        public async Task<IActionResult> UpdateAllTables(List<CCRM2.Shared.Models.DataTable> TablesToUpdate)
        {
            if (TablesToUpdate == null || TablesToUpdate.Count == 0)
            {
                return BadRequest("List of updated dataTables is empty.");
            }
            var currentTabs = await _context.DataTables.ToListAsync();
            foreach (CCRM2.Shared.Models.DataTable tab in TablesToUpdate)
            {
                currentTabs[tab.Id - 1].Selected = tab.Selected;

            }
            await _context.SaveChangesAsync();
            return Ok("Tables updated successfully.");
        }

        #region Exportation of data
        [Authorize(Roles = "Admin,Config")]
        [HttpPost("ExportData")]
        public async Task<IActionResult> ExportDataTables(List<CCRM2.Shared.Models.DataTable> exportTables)
        {
            await UploadDataToBlobAsync(exportTables);
            await RecreateSearchIndexAsync();

            return Ok("Data exported and indexed successfully.");
        }
        private async Task UploadDataToBlobAsync(List<CCRM2.Shared.Models.DataTable> exportTables)
        {
            //uploading all data in selected tables
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            var blobServiceClient = new BlobServiceClient(_blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            foreach (CCRM2.Shared.Models.DataTable tab in exportTables)
            {
                string query = $"SELECT * FROM [{schemaName}].[{tab.Title}]";
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        connection.Open();
                        var table = new System.Data.DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(table);
                        }
                        connection.Close();
                        var json = JsonSerializer.Serialize(DataTableToDictionaryList(table));
                        var data = new BinaryData(json);
                        string fileName = $"{tab.Title}.json";
                        var blobClient = containerClient.GetBlobClient(fileName);
                        await blobClient.UploadAsync(data, overwrite: true);
                    }
                }
            }

            //DB relation json file:
            var schemaDetails = new
            {
                Tables = new List<string>(),
                ForeignKeys = new List<object>()
            };
            string tableQuery = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand tableCommand = new SqlCommand(tableQuery, connection))
                {
                    connection.Open();
                    using (SqlDataReader tableReader = await tableCommand.ExecuteReaderAsync())
                    {
                        while (tableReader.Read())
                        {
                            schemaDetails.Tables.Add(tableReader["TABLE_NAME"].ToString());
                        }
                    }
                    connection.Close();
                }
            }

            string fkQuery = @"
                        SELECT 
                            fk.name AS FK_Name,
                            tp.name AS ParentTable,
                            cp.name AS ParentColumn,
                            tr.name AS ReferencedTable,
                            cr.name AS ReferencedColumn
                        FROM 
                            sys.foreign_keys AS fk
                        INNER JOIN 
                            sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
                        INNER JOIN 
                            sys.tables AS tp ON fkc.parent_object_id = tp.object_id
                        INNER JOIN 
                            sys.columns AS cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
                        INNER JOIN 
                            sys.tables AS tr ON fkc.referenced_object_id = tr.object_id
                        INNER JOIN sys.columns AS cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand fkCommand = new SqlCommand(fkQuery, connection))
                {
                    connection.Open();
                    using (SqlDataReader fkReader = await fkCommand.ExecuteReaderAsync())
                    {
                        while (await fkReader.ReadAsync())
                        {
                            var foreignKey = new
                            {
                                FK_Name = fkReader["FK_Name"].ToString(),
                                ParentTable = fkReader["ParentTable"].ToString(),
                                ParentColumn = fkReader["ParentColumn"].ToString(),
                                ReferencedTable = fkReader["ReferencedTable"].ToString(),
                                ReferencedColumn = fkReader["ReferencedColumn"].ToString()
                            };
                            schemaDetails.ForeignKeys.Add(foreignKey);
                        }
                    }
                    connection.Close();
                }
                // Convert schema details to JSON
                string schemaJson = JsonSerializer.Serialize(schemaDetails, new JsonSerializerOptions { WriteIndented = true });
                var schemaData = new BinaryData(schemaJson);
                Console.WriteLine("Schema data" + schemaData.ToString());
                // Write the schema details to a .json file
                string schemaFileName = "DatabaseRelations.json";

                // Upload the schema file to Azure Blob Storage
                var schemaBlobClient = containerClient.GetBlobClient(schemaFileName);
                await schemaBlobClient.UploadAsync(schemaData, overwrite: true);
                Console.WriteLine("Uploaded blob data");
                Console.WriteLine("Closed connect");
            }
        }


        private List<Dictionary<string, object>> DataTableToDictionaryList(System.Data.DataTable table)
        {
            var columns = table.Columns.Cast<DataColumn>();
            var rows = table.AsEnumerable()
                .Select(row => columns
                    .Select(column => new { Column = column.ColumnName, Value = row[column] })
                    .ToDictionary(data => data.Column, data => data.Value)
                ).ToList();

            return rows;
        }

        private async Task RecreateSearchIndexAsync()
        {
            string indexName = "azuresql-index-tenant-" + _configuration["Auth0:tenant_id"];

            // Delete the existing index if it exists
            await DeleteIndexIfExistsAsync(indexName);
            await CreateIndexAsync(indexName);
            await IndexDataFromBlobAsync(indexName);
        }

        private async Task DeleteIndexIfExistsAsync(string indexName)
        {
            try
            {
                await _indexClient.DeleteIndexAsync(indexName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine("Index not found");
            }
        }

        private async Task CreateIndexAsync(string indexName)
        {
            var index = new SearchIndex(indexName)
            {
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                    new SearchableField("content")
                }
            };

            await _indexClient.CreateOrUpdateIndexAsync(index);
        }

        private async Task IndexDataFromBlobAsync(string indexName)
        {
            var dataSource = new SearchIndexerDataSourceConnection(
                name: $"{indexName}-datasource",
                type: SearchIndexerDataSourceType.AzureBlob,
                connectionString: _blobConnectionString,
                container: new SearchIndexerDataContainer(name: _containerName));

            var indexer = new SearchIndexer(
                name: $"{indexName}-indexer",
                dataSourceName: dataSource.Name,
                targetIndexName: indexName);

            await _indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);
            await _indexerClient.CreateOrUpdateIndexerAsync(indexer);
            await _indexerClient.RunIndexerAsync(indexer.Name);
        }
        #endregion
    }
}
