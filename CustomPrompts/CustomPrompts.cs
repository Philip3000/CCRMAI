﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CCRM2.Shared.Models
{
    [Table("CustomPrompts", Schema = "crm")]
    public partial class CustomPrompts
    {
        [Key]
        public Guid PromptId { get; set; }

        
        public string PromptName { get; set; }

        public string Prompt { get; set; }

        [ForeignKey("Employee")]
        public Guid? EmployeeID { get; set; } // Nullable foreign key

        public virtual Employee Employee { get; set; } // Navigation property

    }
}