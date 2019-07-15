using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SenecaGlobal.VTS.Types
{
    public class Appointment
    {
        public ObjectId _id { get; set; }
        public DateTime appointmentTime { get; set; }
        public DateTime createdDate { get; set; }
        public DateTime modifiedDate { get; set; }
        public string createdBy { get; set; }
        public string instructions { get; set; }
        public string inTime { get; set; }
        public string modifiedBy { get; set; }
        public string organizationName { get; set; }
        public string outTime { get; set; }
        public string status { get; set; }
        public string typeOfVisitor { get; set; }
        public string[] visitorName { get; set; }
        public string whomToMeet { get; set; }
        public string phoneNumber { get; set; }
        public string assets { get; set; }
        public string[] attendedVisitors { get; set; }

    }
}
