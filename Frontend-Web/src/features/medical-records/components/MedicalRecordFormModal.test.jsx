import '@testing-library/jest-dom/vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import MedicalRecordFormModal from './MedicalRecordFormModal'

describe('MedicalRecordFormModal', () => {
  afterEach(cleanup)

  const samplePatients = [
    { patientId: 4, fullName: 'Amal Perera', nic: '199012345678', phoneNumber: '0771234567' },
    { patientId: 7, fullName: 'Sunil Fernando', nic: '198598765432', phoneNumber: '0719876543' },
  ]

  it('correctly maps uploaded PDF to a safe fileUrl path under 1000 chars when submitting', async () => {
    const onSubmit = vi.fn()

    render(
      <MedicalRecordFormModal
        open={true}
        onClose={vi.fn()}
        onSubmit={onSubmit}
        patients={samplePatients}
      />
    )

    // Select patient
    const patientSelect = document.querySelector('select')
    fireEvent.change(patientSelect, { target: { value: '4' } })

    // Fill required fields for Consultation
    fireEvent.change(screen.getByPlaceholderText(/Type 2 Diabetes/i), {
      target: { value: 'Asthma diagnosis' },
    })
    fireEvent.change(screen.getByPlaceholderText(/Enter patient symptoms/i), {
      target: { value: 'Shortness of breath, wheezing' },
    })
    fireEvent.change(screen.getByPlaceholderText(/Enter treatment plan/i), {
      target: { value: 'inhaler twice a day' },
    })

    // Simulate selecting a PDF file
    const file = new File(['%PDF-1.4 dummy content'], 'medical_report.pdf', {
      type: 'application/pdf',
    })
    const fileInput = document.querySelector('input[type="file"]')
    expect(fileInput).toBeInTheDocument()

    fireEvent.change(fileInput, { target: { files: [file] } })

    // Wait for file to appear in the attachments list
    await waitFor(() => {
      expect(screen.getByText('medical_report.pdf')).toBeInTheDocument()
    })

    // Submit form
    const submitBtn = screen.getByRole('button', { name: /create record/i })
    fireEvent.click(submitBtn)

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1)
    })

    const payload = onSubmit.mock.calls[0][0]
    expect(payload.attachments).toHaveLength(1)
    expect(payload.attachments[0].fileName).toBe('medical_report.pdf')
    expect(payload.attachments[0].fileType).toBe('application/pdf')
    expect(payload.attachments[0].fileUrl).toBe('/uploads/medical-records/medical_report.pdf')
    expect(payload.attachments[0].fileUrl.length).toBeLessThan(1000)
    expect(payload.attachments[0].fileSize).toBeGreaterThan(0)
  })

  it('filters patients in search input and selects patient', async () => {
    render(
      <MedicalRecordFormModal
        open={true}
        onClose={vi.fn()}
        onSubmit={vi.fn()}
        patients={samplePatients}
      />
    )

    const searchInput = screen.getByPlaceholderText(/Search patient by name, NIC/i)
    expect(searchInput).toBeInTheDocument()

    // Type query to find Sunil
    fireEvent.change(searchInput, { target: { value: 'Sunil' } })

    // Sunil option should be visible in the filtered list
    const patientOption = await screen.findByTestId('patient-option-7')
    expect(patientOption).toBeInTheDocument()
    expect(patientOption).toHaveTextContent('Sunil Fernando')

    // Click to select
    fireEvent.click(patientOption)

    expect(searchInput.value).toBe('Sunil Fernando')
  })

  it('dynamically adapts fields when record type changes to LabReport', async () => {
    render(
      <MedicalRecordFormModal
        open={true}
        onClose={vi.fn()}
        onSubmit={vi.fn()}
        patients={samplePatients}
      />
    )

    // Find Record Type select
    const selects = screen.getAllByRole('combobox')
    const typeSelect = selects.find((s) => s.querySelector('option[value="LabReport"]'))
    expect(typeSelect).toBeInTheDocument()

    fireEvent.change(typeSelect, { target: { value: 'LabReport' } })

    // LabReport specific fields should now be present
    expect(screen.getByText(/Laboratory Investigation & Metrics/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Full Blood Count \(FBC\)/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/WBC: 7.2 x10\^3\/uL/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/biological reference intervals/i)).toBeInTheDocument()

    // Consultation symptoms field should not be visible
    expect(screen.queryByPlaceholderText(/Enter patient symptoms and complaints/i)).not.toBeInTheDocument()
  })

  it('dynamically adapts fields when record type changes to Prescription', async () => {
    render(
      <MedicalRecordFormModal
        open={true}
        onClose={vi.fn()}
        onSubmit={vi.fn()}
        patients={samplePatients}
      />
    )

    const selects = screen.getAllByRole('combobox')
    const typeSelect = selects.find((s) => s.querySelector('option[value="Prescription"]'))
    expect(typeSelect).toBeInTheDocument()

    fireEvent.change(typeSelect, { target: { value: 'Prescription' } })

    // Prescription specific fields should now be present
    expect(screen.getByText(/Prescription Details & Regimen/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Acute Bacterial Sinusitis/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Amoxicillin\/Clavulanate 625mg/i)).toBeInTheDocument()
    expect(screen.getByPlaceholderText(/Take immediately after meals/i)).toBeInTheDocument()
  })
})
