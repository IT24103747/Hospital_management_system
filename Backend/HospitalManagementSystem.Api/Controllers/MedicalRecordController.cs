using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly IFileStorageService _fileStorageService;

        public MedicalRecordController(
            IMedicalRecordService service,
            ILogger<MedicalRecordController> logger,
            IFileStorageService fileStorageService)
        {
            _service = service;
            _logger = logger;
            _fileStorageService = fileStorageService;
        }

        private (bool isAdmin, bool isDoctor, bool isPatient, string? email) GetUserContext()
        {
            var roleClaim = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            bool isAdmin = User.IsInRole("Admin") || string.Equals(roleClaim, "Admin", StringComparison.OrdinalIgnoreCase);
            bool isDoctor = User.IsInRole("Doctor") || string.Equals(roleClaim, "Doctor", StringComparison.OrdinalIgnoreCase);
            bool isPatient = User.IsInRole("Patient") || string.Equals(roleClaim, "Patient", StringComparison.OrdinalIgnoreCase) || (!isAdmin && !isDoctor);
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
            return (isAdmin, isDoctor, isPatient, email);
        }

        // GET /api/medicalrecord?patientId=1&recordType=Consultation&status=Finalized&page=1&pageSize=10
        [HttpGet]
        [Authorize(Roles = "Admin,Doctor,Patient")]
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
            var (isAdmin, isDoctor, _, email) = GetUserContext();

            // Strict privacy: If caller is not Admin or Doctor, they can ONLY view their own records
            if (!isAdmin && !isDoctor)
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Unauthorized(new { message = "Token missing email claim." });

                var myPatientId = await _service.GetPatientIdByEmailAsync(email);
                if (!myPatientId.HasValue)
                {
                    return Ok(new PagedResult<MedicalRecordDto>
                    {
                        Data = new List<MedicalRecordDto>(),
                        TotalCount = 0,
                        Page = page,
                        PageSize = pageSize
                    });
                }
                // Strictly overwrite any client-supplied patientId with caller's own patientId
                patientId = myPatientId.Value;
            }
            else if (isDoctor && !isAdmin)
            {
                if (string.IsNullOrWhiteSpace(email))
                    return Unauthorized(new { message = "Token missing email claim." });

                var myDoctorId = await _service.GetDoctorIdByEmailAsync(email);
                if (!myDoctorId.HasValue)
                {
                    return Ok(new PagedResult<MedicalRecordDto>
                    {
                        Data = new List<MedicalRecordDto>(),
                        TotalCount = 0,
                        Page = page,
                        PageSize = pageSize
                    });
                }
                // Strictly overwrite doctorId so doctors only view records assigned to their own doctor profile
                doctorId = myDoctorId.Value;
            }

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
            var (isAdmin, isDoctor, _, email) = GetUserContext();
            int? doctorId = null;

            if (isDoctor && !isAdmin && !string.IsNullOrWhiteSpace(email))
            {
                doctorId = await _service.GetDoctorIdByEmailAsync(email);
                if (!doctorId.HasValue)
                    return Ok(new MedicalRecordSummaryDto());
            }

            var summary = await _service.GetSummaryAsync(doctorId);
            return Ok(summary);
        }

        // GET /api/medicalrecord/me
        [HttpGet("me")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        [ProducesResponseType(typeof(IEnumerable<MedicalRecordDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyMedicalRecords()
        {
            var (isAdmin, isDoctor, _, email) = GetUserContext();
            if (string.IsNullOrWhiteSpace(email))
                return Unauthorized(new { message = "Token missing email claim." });

            if (isAdmin)
            {
                var all = await _service.GetAllRecordsAsync(null, null, null, null, null, null, null, null, null, 1, 100);
                return Ok(all.Data);
            }

            if (isDoctor)
            {
                var myDoctorId = await _service.GetDoctorIdByEmailAsync(email);
                if (!myDoctorId.HasValue)
                    return Ok(new List<MedicalRecordDto>());

                var docRecords = await _service.GetAllRecordsAsync(null, myDoctorId.Value, null, null, null, null, null, null, null, 1, 100);
                return Ok(docRecords.Data);
            }

            var records = await _service.GetMyMedicalRecordsAsync(email);
            if (records == null)
                return Ok(new List<MedicalRecordDto>());

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
            var (isAdmin, isDoctor, _, email) = GetUserContext();
            var role = isAdmin ? "Admin" : (isDoctor ? "Doctor" : "Patient");

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

            var (isAdmin, isDoctor, _, email) = GetUserContext();
            if (!isAdmin && !isDoctor)
            {
                if (string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(record.PatientEmail) ||
                    !string.Equals(record.PatientEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to view this medical record." });
                }
            }
            else if (isDoctor && !isAdmin)
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    var myDoctorId = await _service.GetDoctorIdByEmailAsync(email);
                    if (!myDoctorId.HasValue || record.DoctorId != myDoctorId.Value)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have access to view this medical record." });
                    }
                }
            }

            return Ok(record);
        }

        // POST /api/medicalrecord
        [HttpPost]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        [ProducesResponseType(typeof(MedicalRecordDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] CreateMedicalRecordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var (isAdmin, isDoctor, _, email) = GetUserContext();
            var role = isAdmin ? "Admin" : (isDoctor ? "Doctor" : "Patient");

            try
            {
                var created = await _service.CreateRecordAsync(dto, email, role);
                _logger.LogInformation("Medical record {Id} created for patient {PatientId}", created.MedicalRecordId, created.PatientId);
                return CreatedAtAction(nameof(GetById), new { id = created.MedicalRecordId }, created);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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

        // POST /api/medicalrecord/{id}/upload-attachment
        [HttpPost("{id:int}/upload-attachment")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(MedicalRecordAttachmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadAttachment(int id, IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file was uploaded." });

            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(new { message = "File size exceeds the 50 MB limit." });

            var record = await _service.GetRecordByIdAsync(id);
            if (record == null)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            var (isAdmin, isDoctor, _, email) = GetUserContext();
            if (!isAdmin && !isDoctor)
            {
                if (string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(record.PatientEmail) ||
                    !string.Equals(record.PatientEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot attach files to another patient's record." });
                }
            }

            var safeOriginalName = Path.GetFileName(file.FileName);
            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

            string uploadedUrl;
            using (var stream = file.OpenReadStream())
            {
                uploadedUrl = await _fileStorageService.UploadAsync(stream, safeOriginalName, contentType, "medical-records");
            }

            var dto = new CreateAttachmentDto
            {
                FileName = safeOriginalName,
                FileType = contentType,
                FileUrl = uploadedUrl,
                FileSize = file.Length
            };

            var attachment = await _service.AddAttachmentAsync(id, dto);
            if (attachment == null)
                return NotFound(new { message = $"Medical record with ID {id} was not found." });

            _logger.LogInformation("Attachment file {FileName} uploaded for medical record {RecordId} to {FileUrl}", safeOriginalName, id, uploadedUrl);
            return StatusCode(StatusCodes.Status201Created, attachment);
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

            var (isAdmin, isDoctor, _, email) = GetUserContext();
            if (!isAdmin && !isDoctor)
            {
                if (string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(record.PatientEmail) ||
                    !string.Equals(record.PatientEmail.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "You cannot attach files to another patient's record." });
                }
            }

            // If the client provided a base64 data URL, upload it to cloud storage
            if (!string.IsNullOrWhiteSpace(dto.FileUrl) && dto.FileUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var commaIndex = dto.FileUrl.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        var base64Data = dto.FileUrl[(commaIndex + 1)..];
                        var fileBytes = Convert.FromBase64String(base64Data);
                        var safeName = Path.GetFileName(dto.FileName);
                        if (string.IsNullOrWhiteSpace(safeName)) safeName = "attachment.bin";

                        var mime = "application/octet-stream";
                        var semiIndex = dto.FileUrl.IndexOf(';');
                        if (semiIndex > 5)
                        {
                            mime = dto.FileUrl[5..semiIndex];
                        }

                        var uploadedUrl = await _fileStorageService.UploadBytesAsync(fileBytes, safeName, mime, "medical-records");
                        dto.FileUrl = uploadedUrl;
                        dto.FileSize = fileBytes.Length;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decode base64 file for attachment {FileName}", dto.FileName);
                }
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
            var record = await _service.GetRecordByIdAsync(id);
            var attachment = record?.Attachments?.Find(a => a.AttachmentId == attachmentId);

            var deleted = await _service.DeleteAttachmentAsync(id, attachmentId);
            if (!deleted)
                return NotFound(new { message = $"Attachment with ID {attachmentId} for medical record {id} was not found." });

            if (attachment != null && !string.IsNullOrWhiteSpace(attachment.FileUrl))
            {
                await _fileStorageService.DeleteAsync(attachment.FileUrl);
            }

            _logger.LogInformation("Attachment {AttachmentId} removed from record {RecordId}", attachmentId, id);
            return NoContent();
        }

        // GET /api/medicalrecord/{id}/attachments/{attachmentId}/download
        [HttpGet("{id:int}/attachments/{attachmentId:int}/download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
        {
            var record = await _service.GetRecordByIdAsync(id);
            if (record == null)
                return NotFound(new { message = $"Medical record #{id} was not found." });

            var attachment = record.Attachments?.Find(a => a.AttachmentId == attachmentId);
            if (attachment == null)
                return NotFound(new { message = $"Attachment #{attachmentId} was not found." });

            // Redirect directly to the Cloudflare R2 / remote CDN URL
            if (attachment.FileUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                attachment.FileUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(attachment.FileUrl);
            }

            return NotFound(new { message = "Attachment file not found." });
        }
    }
}
