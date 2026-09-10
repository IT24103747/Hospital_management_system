using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public class MedicalRecordRepository : IMedicalRecordRepository
    {
        private readonly ApplicationDbContext _db;

        public MedicalRecordRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<MedicalRecord>> GetAllAsync(
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
            var query = ApplyFilters(_db.MedicalRecords.AsNoTracking()
                .Include(m => m.Patient)
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Attachments),
                patientId, doctorId, recordType, status, search, fromDate, toDate);

            // Sorting
            bool desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            query = (sortBy?.ToLower()) switch
            {
                "patient" => desc ? query.OrderByDescending(m => m.Patient != null ? m.Patient.FirstName : "") : query.OrderBy(m => m.Patient != null ? m.Patient.FirstName : ""),
                "type" => desc ? query.OrderByDescending(m => m.RecordType) : query.OrderBy(m => m.RecordType),
                "status" => desc ? query.OrderByDescending(m => m.Status) : query.OrderBy(m => m.Status),
                "date" => desc ? query.OrderByDescending(m => m.RecordDate) : query.OrderBy(m => m.RecordDate),
                _ => desc ? query.OrderByDescending(m => m.RecordDate).ThenByDescending(m => m.MedicalRecordId)
                          : query.OrderByDescending(m => m.RecordDate).ThenByDescending(m => m.MedicalRecordId)
            };

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = ApplyFilters(_db.MedicalRecords.AsNoTracking(),
                patientId, doctorId, recordType, status, search, fromDate, toDate);
            return await query.CountAsync();
        }

        public async Task<MedicalRecord?> GetByIdAsync(int id)
        {
            return await _db.MedicalRecords
                .Include(m => m.Patient)
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.MedicalRecordId == id);
        }

        public async Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId)
        {
            return await _db.MedicalRecords.AsNoTracking()
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Attachments)
                .Where(m => m.PatientId == patientId)
                .OrderByDescending(m => m.RecordDate)
                .ToListAsync();
        }

        public async Task<MedicalRecordSummaryDto> GetSummaryAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var total = await _db.MedicalRecords.CountAsync();
            var consultation = await _db.MedicalRecords.CountAsync(m => m.RecordType == MedicalRecordTypes.Consultation);
            var lab = await _db.MedicalRecords.CountAsync(m => m.RecordType == MedicalRecordTypes.LabReport);
            var discharge = await _db.MedicalRecords.CountAsync(m => m.RecordType == MedicalRecordTypes.DischargeSummary);
            var prescription = await _db.MedicalRecords.CountAsync(m => m.RecordType == MedicalRecordTypes.Prescription);
            var general = await _db.MedicalRecords.CountAsync(m => m.RecordType == MedicalRecordTypes.GeneralNote);
            var draft = await _db.MedicalRecords.CountAsync(m => m.Status == MedicalRecordStatuses.Draft);
            var finalized = await _db.MedicalRecords.CountAsync(m => m.Status == MedicalRecordStatuses.Finalized);
            var thisMonth = await _db.MedicalRecords.CountAsync(m => m.CreatedAt >= startOfMonth);

            return new MedicalRecordSummaryDto
            {
                TotalRecords = total,
                ConsultationCount = consultation,
                LabReportCount = lab,
                DischargeSummaryCount = discharge,
                PrescriptionCount = prescription,
                GeneralNoteCount = general,
                DraftCount = draft,
                FinalizedCount = finalized,
                AddedThisMonth = thisMonth
            };
        }

        public async Task<MedicalRecord> CreateAsync(MedicalRecord record)
        {
            _db.MedicalRecords.Add(record);
            await _db.SaveChangesAsync();
            return record;
        }

        public async Task<MedicalRecord> UpdateAsync(MedicalRecord record)
        {
            record.UpdatedAt = DateTime.UtcNow;
            _db.MedicalRecords.Update(record);
            await _db.SaveChangesAsync();
            return record;
        }

        public async Task DeleteAsync(MedicalRecord record)
        {
            _db.MedicalRecords.Remove(record);
            await _db.SaveChangesAsync();
        }

        public async Task<MedicalRecordAttachment> AddAttachmentAsync(MedicalRecordAttachment attachment)
        {
            _db.MedicalRecordAttachments.Add(attachment);
            await _db.SaveChangesAsync();
            return attachment;
        }

        public async Task<MedicalRecordAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _db.MedicalRecordAttachments.FindAsync(attachmentId);
        }

        public async Task DeleteAttachmentAsync(MedicalRecordAttachment attachment)
        {
            _db.MedicalRecordAttachments.Remove(attachment);
            await _db.SaveChangesAsync();
        }

        private static IQueryable<MedicalRecord> ApplyFilters(
            IQueryable<MedicalRecord> query,
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (patientId.HasValue)
                query = query.Where(m => m.PatientId == patientId.Value);

            if (doctorId.HasValue)
                query = query.Where(m => m.DoctorId == doctorId.Value);

            if (!string.IsNullOrWhiteSpace(recordType))
                query = query.Where(m => m.RecordType == recordType);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(m => m.Status == status);

            if (fromDate.HasValue)
                query = query.Where(m => m.RecordDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(m => m.RecordDate <= toDate.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(m =>
                    m.Diagnosis.ToLower().Contains(s) ||
                    m.Symptoms.ToLower().Contains(s) ||
                    m.TreatmentPlan.ToLower().Contains(s) ||
                    (m.Patient != null && (m.Patient.FirstName.ToLower().Contains(s) || m.Patient.LastName.ToLower().Contains(s) || m.Patient.NIC.ToLower().Contains(s))));
            }

            return query;
        }
    }
}
