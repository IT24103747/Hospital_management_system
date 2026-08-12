import apiClient from '../../../lib/apiClient'

export const getDoctorRegistrations = async status => (await apiClient.get('/admin/doctor-registrations', { params: status ? { status } : {} })).data
export const approveDoctor = async id => (await apiClient.post(`/admin/doctor-registrations/${id}/approve`)).data
export const declineDoctor = async (id, reason) => (await apiClient.post(`/admin/doctor-registrations/${id}/decline`, { reason })).data
export const getMyDoctorProfile = async () => (await apiClient.get('/doctors/me')).data
export const updateMyDoctorProfile = async data => (await apiClient.put('/doctors/me', data)).data
