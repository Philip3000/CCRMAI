using CCRM2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace CCRM2.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomPromptsController : ControllerBase
    {
        private readonly CCRM2DBContext _context;

        public CustomPromptsController(CCRM2DBContext context)
        {
            _context = context;
        }


        [Authorize(Roles = "Admin,Config")]
        [HttpGet()]
        public async Task<ActionResult<IEnumerable<CustomPrompts>>> GetCustomPrompts()
        {
            return await _context.CustomPrompts.ToListAsync();
        }

        [Authorize(Roles = "Admin,Config")]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomPrompts(Guid id, CustomPrompts updatedPrompt)
        {
            if (id != updatedPrompt.PromptId)
            {
                return BadRequest("Error - Something went wrong");
            }

            var existingPrompt = await _context.CustomPrompts.FindAsync(id);

            if (existingPrompt == null)
            {
                return NotFound("Couldn't find prompt");
            }

            existingPrompt.PromptName = updatedPrompt.PromptName;
            existingPrompt.Prompt = updatedPrompt.Prompt;
            try
            {
                existingPrompt.PromptName = existingPrompt.PromptName?.Trim();

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomPromptsExists(id))
                {
                    return NotFound("Couldn't find prompt");
                }
                else
                {
                    throw;
                }
            }

            return Ok("Updated");
        }

      

        [Authorize(Roles = "Admin,Config")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomPrompts([FromRoute] Guid id)
        {
            var customPrompt = await _context.CustomPrompts.FindAsync(id);

            if (customPrompt == null)
            {
                return NotFound("Couldn't find prompt with id: " + id);
            }

            _context.CustomPrompts.Remove(customPrompt);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception e)
            {
                return BadRequest("Error deleting custom prompt: " + e.Message);
            }

            return Ok("Succesfully deleted prompt");
        }

        [Authorize(Roles = "Admin,Config")]
        [HttpPost]
        public async Task<ActionResult<CustomPrompts>> PostCustomPrompts(CustomPrompts customPrompts)
        {
            try
            {
                customPrompts.PromptName = customPrompts.PromptName?.Trim();

                customPrompts.Prompt = customPrompts.Prompt?.Trim();

                // Assuming you have some logic to set other properties or perform validation

                _context.CustomPrompts.Add(customPrompts);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message + ". Inner exception: " + ex.InnerException.Message);
            }
        }

        private bool CustomPromptsExists(Guid id)
        {
            return _context.CustomPrompts.Any(e => e.PromptId == id);
        }

    }
}
