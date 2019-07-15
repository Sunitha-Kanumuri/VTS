using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Types
{
    public class AppointmentData {

        public string _id { get; set; }
        public string appointmentTime { get; set; }
        public string instructions { get; set; }
        public string inTime { get; set; }
        public string organizationName { get; set; }
        public string outTime { get; set; }
        public string status { get; set; }
        public string typeOfVisitor { get; set; }
        public string[] visitorName { get; set; }
        public string whomToMeet { get; set; }
        public string phoneNumber { get; set; }
        public int? pageNo { get; set; }
        public int? pageSize { get; set; }
        public string searchKey { get; set; }
        public string startDate { get; set; }
        public string endDate { get; set; }
        public string scope { get; set; }
        public string sortField { get; set; }
        public string assets { get; set; }
        public string[] attendedVisitors { get; set; }
    }
}
