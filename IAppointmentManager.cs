using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Types.Interfaces
{
    public interface IAppointmentManager
    {
        Task<ScrollList<Appointment>> getAppointmentsAsync(string currentUser, string searchKey, string status, string startDate, string endDate, string scope, string appointmentTime, string sortField, int? pageNo = null, int? pageSize = null);
        Task<Appointment> createAppointmentAsync(AppointmentData appointment, string currentUser);
        Task<long> updateAppointmentAsync(AppointmentData appointment);
        Task<long> deleteAppointmentAsync();
        Task<List<string>> suggestVisitorName(string searchString);
        Task<long> checkInAsync(string Id, string assets,string[] attendedVisitors);
        Task<string> checkOutAsync(string Id);
        Task<long> updateAssets(string Id, string assets);
        Task<byte[]> printAppointments(AppointmentData appointment, string currentUser);
    }
}
