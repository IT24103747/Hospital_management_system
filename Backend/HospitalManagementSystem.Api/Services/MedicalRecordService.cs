using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly IMedicalRecordRepository _repo;
        private readonly ApplicationDbContext _db;

        public MedicalRecordService(IMedicalRecordRepository repo, ApplicationDbContext db)
        {
            _repo = repo;
            _db = db;
        }

        public async Task<PagedResult<MedicalRecordDto>> GetAllRecordsAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var total = await _repo.GetTotalCountAsync(patientId, doctorId, recordType, status, search, fromDate, toDate);
            var records = await _repo.GetAllAsync(patientId, doctorId, recordType, status, search, fromDate, toDate, sortBy, sortDirection, page, pageSize);

            return new PagedResult<MedicalRecordDto>
            {
                Data = records.Select(MapToDto),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MedicalRecordDto?> GetRecordByIdAsync(int id)
        {
            var record = await _repo.GetByIdAsync(id);
            return record == null ? null : MapToDto(record);
        }

        public async Task<IEnumerable<MedicalRecordDto>?> GetPatientMedicalHistoryAsync(int patientId, string? userEmail, string? userRole)
        {
            var patient = await _db.Patients.FindAsync(patientId);
            if (patient == null)
                return null;

            // Role scope verification: Patients can only access their own history
            if (string.Equals(userRole, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(patient.Email, userEmail, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("You are not authorized to view another patient's medical history.");
            }

            var records = await _repo.GetByPatientIdAsync(patientId);
            return records.Select(MapToDto);
        }

        public async Task<IEnumerable<MedicalRecordDto>?> GetMyMedicalRecordsAsync(string patientEmail)
        {
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Email == patientEmail);
            if (patient == null)
                return null;

            var records = await _repo.GetByPatientIdAsync(patient.PatientId);
            return records.Select(MapToDto);
        }

        public async Task<MedicalRecordSummaryDto> GetSummaryAsync()
        {
            return await _repo.GetSummaryAsync();
        }

        public async Task<MedicalRecordDto> CreateRecordAsync(CreateMedicalRecordDto dto, string? userEmail, string? userRole)
        {
            var patient = await _db.Patients.FindAsync(dto.PatientId);
            if (patient == null)
                throw new KeyNotFoundException($"Patient with ID {dto.PatientId} was not found.");

            int? doctorId = dto.DoctorId;

            // If user is Doctor and doctorId wasn't explicitly supplied, resolve their DoctorId from their user account
            if (!doctorId.HasValue && string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(userEmail))
            {
                var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.User.Email == userEmail);
                if (doctor != null)
                    doctorId = doctor.DoctorId;
            }

            var record = new MedicalRecord
            {
                PatientId = dto.PatientId,
                DoctorId = doctorId,
                AppointmentId = dto.AppointmentId,
                RecordDate = dto.RecordDate ?? DateTime.UtcNow,
                RecordType = string.IsNullOrWhiteSpace(dto.RecordType) ? MedicalRecordTypes.Consultation : dto.RecordType,
                Diagnosis = dto.Diagnosis.Trim(),
                Symptoms = dto.Symptoms.Trim(),
                TreatmentPlan = dto.TreatmentPlan.Trim(),
                PrescriptionNotes = dto.PrescriptionNotes?.Trim(),
                LabNotes = dto.LabNotes?.Trim(),
                FollowUpDate = dto.FollowUpDate,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? MedicalRecordStatuses.Finalized : dto.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (dto.Attachments != null && dto.Attachments.Any())
            {
                foreach (var att in dto.Attachments)
                {
                    record.Attachments.Add(new MedicalRecordAttachment
                    {
                        FileName = att.FileName.Trim(),
                        FileType = att.FileType.Trim(),
                        FileUrl = att.FileUrl.Trim(),
                        FileSize = att.FileSize,
                        UploadedAt = DateTime.UtcNow
                    });
                }
            }

            var created = await _repo.CreateAsync(record);
            var loaded = await _repo.GetByIdAsync(created.MedicalRecordId);
            return MapToDto(loaded ?? created);
        }

        public async Task<MedicalRecordDto?> UpdateRecordAsync(int id, UpdateMedicalRecordDto dto)
        {
            var record = await _repo.GetByIdAsync(id);
            if (record == null)
                return null;

            record.RecordType = dto.RecordType;
            record.Diagnosis = dto.Diagnosis.Trim();
            record.Symptoms = dto.Symptoms.Trim();
            record.TreatmentPlan = dto.TreatmentPlan.Trim();
            record.PrescriptionNotes = dto.PrescriptionNotes?.Trim();
            record.LabNotes = dto.LabNotes?.Trim();
            record.FollowUpDate = dto.FollowUpDate;
            record.Status = dto.Status;

            var updated = await _repo.UpdateAsync(record);
            return MapToDto(updated);
        }

        public async Task<bool> DeleteRecordAsync(int id)
        {
            var record = await _repo.GetByIdAsync(id);
            if (record == null)
                return false;

            await _repo.DeleteAsync(record);
            return true;
        }

        public async Task<MedicalRecordAttachmentDto?> AddAttachmentAsync(int recordId, CreateAttachmentDto dto)
        {
            var record = await _repo.GetByIdAsync(recordId);
            if (record == null)
                return null;

            var attachment = new MedicalRecordAttachment
            {
                MedicalRecordId = recordId,
                FileName = dto.FileName.Trim(),
                FileType = dto.FileType.Trim(),
                FileUrl = dto.FileUrl.Trim(),
                FileSize = dto.FileSize,
                UploadedAt = DateTime.UtcNow
            };

            var created = await _repo.AddAttachmentAsync(attachment);
            return new MedicalRecordAttachmentDto
            {
                AttachmentId = created.AttachmentId,
                MedicalRecordId = created.MedicalRecordId,
                FileName = created.FileName,
                FileType = created.FileType,
                FileUrl = created.FileUrl,
                FileSize = created.FileSize,
                UploadedAt = created.UploadedAt
            };
        }

        public async Task<bool> DeleteAttachmentAsync(int recordId, int attachmentId)
        {
            var attachment = await _repo.GetAttachmentByIdAsync(attachmentId);
            if (attachment == null || attachment.MedicalRecordId != recordId)
                return false;

            await _repo.DeleteAttachmentAsync(attachment);
            return true;
        }

        private static MedicalRecordDto MapToDto(MedicalRecord m)
        {
            return new MedicalRecordDto
            {
                MedicalRecordId = m.MedicalRecordId,
                PatientId = m.PatientId,
                PatientName = m.Patient != null ? $"{m.Patient.FirstName} {m.Patient.LastName}".Trim() : string.Empty,
                PatientEmail = m.Patient?.Email ?? string.Empty,
                DoctorId = m.DoctorId,
                DoctorName = m.Doctor != null ? $"Dr. {m.Doctor.FirstName} {m.Doctor.LastName}".Trim() : null,
                DoctorSpecialization = m.Doctor?.Specialization,
                AppointmentId = m.AppointmentId,
                RecordDate = m.RecordDate,
                RecordType = m.RecordType,
                Diagnosis = m.Diagnosis,
                Symptoms = m.Symptoms,
                TreatmentPlan = m.TreatmentPlan,
                PrescriptionNotes = m.PrescriptionNotes,
                LabNotes = m.LabNotes,
                FollowUpDate = m.FollowUpDate,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                Attachments = m.Attachments?.Select(a => new MedicalRecordAttachmentDto
                {
                    AttachmentId = a.AttachmentId,
                    MedicalRecordId = a.MedicalRecordId,
                    FileName = a.FileName,
                    FileType = a.FileType,
                    FileUrl = a.FileUrl,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList() ?? new List<MedicalRecordAttachmentDto>()
            };
        }
    }
}
