using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MedicalRecordController : ControllerBase
    {
        private readonly IMedicalRecordService _service;
        private readonly ILogger<MedicalRecordController> _logger;

        public MedicalRecordController(IMedicalRecordService service, ILogger<MedicalRecordController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET /api/medicalrecord?patientId=1&recordType=Consultation&status=Finalized&page=1&pageSize=10
        [HttpGet]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(PagedResult<MedicalRecordDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? patientId,
            [FromQuery] int? doctorId,
            [FromQuery] string? recordType,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllRecordsAsync(
                patientId, doctorId, recordType, status, search, fromDate, toDate, sortBy, sortDirection, page, pageSize);
            return Ok(result);
        }

        // GET /api/medicalrecord/summary
        [HttpGet("summary")]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(MedicalRecordSummaryDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSummary()
        {
            var summary = await _service.GetSummaryAsync();
            return Ok(summary);
        }

        // GET /api/medicalrecord/me
        [HttpGet("me")]
        [Authorize(Roles = "Patient")]
        [ProducesResponseType(typeof(IEnumerable<MedicalRecordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMyMedicalRecords()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { message = "Token missing email claim." });

            var records = await _service.GetMyMedicalRecordsAsync(email);
            if (records == null)
                return NotFound(new { message = "No patient profile associated with this user." });

            return Ok(records);
        }

        // GET /api/medicalrecord/patient/{patientId}
        [HttpGet("patient/{patientId:int}")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        [ProducesResponseType(typeof(IEnumerable<MedicalRecordDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPatientHistory(int patientId)
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var role = User.FindFirstValue(ClaimTypes.Role) ?? (User.IsInRole("Admin") ? "Admin" : User.IsInRole("Doctor") ? "Doctor" : "Patient");

            try
            {
                var history = await _service.GetPatientMedicalHistoryAsync(patientId, email, role);
                if (history == null)
                    return NotFound(new { message = $"Patient with ID {patientId} was not found." });

                return Ok(history);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
        }

        // GET /api/medicalrecord/{id}
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        [ProducesResponseType(typeof(MedicalRecordDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var record = await _service.GetRecordByIdAsync(id);
            if (record == null)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            // If user is a patient, verify they own this record
            if (User.IsInRole("Patient"))
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (!string.Equals(record.PatientEmail, email, StringComparison.OrdinalIgnoreCase))
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to view this medical record." });
            }

            return Ok(record);
        }

        // POST /api/medicalrecord
        [HttpPost]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(MedicalRecordDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateMedicalRecordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = User.FindFirstValue(ClaimTypes.Email);
            var role = User.IsInRole("Admin") ? "Admin" : "Doctor";

            try
            {
                var created = await _service.CreateRecordAsync(dto, email, role);
                _logger.LogInformation("Medical record {Id} created for patient {PatientId}", created.MedicalRecordId, created.PatientId);
                return CreatedAtAction(nameof(GetById), new { id = created.MedicalRecordId }, created);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // PUT /api/medicalrecord/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(typeof(MedicalRecordDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMedicalRecordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = await _service.UpdateRecordAsync(id, dto);
            if (updated == null)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            _logger.LogInformation("Medical record {Id} updated", id);
            return Ok(updated);
        }

        // DELETE /api/medicalrecord/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteRecordAsync(id);
            if (!deleted)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            _logger.LogInformation("Medical record {Id} deleted by Admin", id);
            return NoContent();
        }

        // POST /api/medicalrecord/{id}/attachments
        [HttpPost("{id:int}/attachments")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        [ProducesResponseType(typeof(MedicalRecordAttachmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddAttachment(int id, [FromBody] CreateAttachmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var record = await _service.GetRecordByIdAsync(id);
            if (record == null)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            if (User.IsInRole("Patient"))
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (!string.Equals(record.PatientEmail, email, StringComparison.OrdinalIgnoreCase))
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot attach files to another patient's record." });
            }

            var attachment = await _service.AddAttachmentAsync(id, dto);
            if (attachment == null)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            _logger.LogInformation("Attachment {AttachmentId} added to medical record {RecordId}", attachment.AttachmentId, id);
            return StatusCode(StatusCodes.Status201Created, attachment);
        }

        // DELETE /api/medicalrecord/{id}/attachments/{attachmentId}
        [HttpDelete("{id:int}/attachments/{attachmentId:int}")]
        [Authorize(Roles = "Admin,Doctor")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAttachment(int id, int attachmentId)
        {
            var deleted = await _service.DeleteAttachmentAsync(id, attachmentId);
            if (!deleted)
                return NotFound(new { message = $"Attachment with ID {attachmentId} for medical record {id} was not found." });

            _logger.LogInformation("Attachment {AttachmentId} removed from record {RecordId}", attachmentId, id);
            return NoContent();
        }
    }
}
