using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SenecaGlobal.VTS.Types;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices;
using SenecaGlobal.VTS.Types.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json.Linq;

namespace SenecaGlobal.VTS.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class SecurityAppointmentController : Controller
    {
        private const string _domainName = "senecaglobal.net";
        private const string _organizationalUnit = "OU=Seneca";
        private const string _samAccountName = "samAccountName";
        private const string _memberOf = "memberOf";
        private const string _vtsSecurity = "vtsSecurity";
        private const string _vtsAdmin = "vtsAdmin";
        private const string _vtsTestUsers = "vtsTestUsers";
        private const string displayName = "displayName";

        private IAppointmentManager appointmentmanager;

        public SecurityAppointmentController(IAppointmentManager appmanager)
        {
            appointmentmanager = appmanager;
        }

        [HttpGet("getActiveDirectoryUsers")]
        public IActionResult getActiveDirectoryUsers()
        {
            List<string> users = null;
            using (var context = new PrincipalContext(ContextType.Domain, _domainName))
            {
                var userIdentity = new UserPrincipal(context);
                userIdentity.DelegationPermitted = true;
                using (var searcher = new PrincipalSearcher(userIdentity))
                {
                    users = new List<string>();
                    foreach (var result in searcher.FindAll())
                    {
                        DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;

                        if (!de.Path.Contains(_organizationalUnit))
                            continue;
                        string user = (string)de.Properties[displayName].Value;
                        users.Add(user);
                    }
                }
            }
            if (users == null || users.Count() == 0)
                return NotFound();

            users = users.OrderBy(e => e).ToList();
            return new ObjectResult(users);
        }
        [HttpGet("suggestVisitorName")]
        public async Task<List<string>> suggestVisitorName(string searchString)
        {
            return await appointmentmanager.suggestVisitorName(searchString);
        }

        [HttpPost("getAppointments")]
        public async Task<ScrollList<Appointment>> getAppointmentsAsync([FromBody] dynamic appointmentData)
        {
            AppointmentData appointment = JsonConvert.DeserializeObject<AppointmentData>(appointmentData.AppointmentData.ToString());
            string currentUser = getLoggedInUser(userRoleRequired: false);
            return await appointmentmanager.getAppointmentsAsync(currentUser, appointment.searchKey, appointment.status, appointment.startDate, appointment.endDate, appointment.scope, appointment.appointmentTime, appointment.sortField, appointment.pageNo, appointment.pageSize);
        }

        // GET api/appointment/5
        [HttpGet("{id}", Name = "getSecurityAppointmentById")]
        public string getAppointmentById(int id)
        {
            return "value";
        }

        [HttpGet("getLoggedInUser")]
        public string getLoggedInUser(bool userRoleRequired = true)
        {
            string name = User.Identity.Name;

            if (string.IsNullOrWhiteSpace(name))
                name = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            name = name.Split('\\')[1];

            if (name.IndexOf('.') != -1) name = name.Replace('.', ' ');

            if (!userRoleRequired) return name;

            string ip = HttpContext.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress?.ToString();

            string role = getUserRole(name);
            return $"{name}|{role}";
        }

        [HttpPost("printAppointments")]
        public async Task<FileResult> printAppointmentsAsync([FromBody] dynamic appointmentData)
        {
            AppointmentData appointment = JsonConvert.DeserializeObject<AppointmentData>(appointmentData.AppointmentData.ToString());
            string currentUser = getLoggedInUser(userRoleRequired: false);

            byte[] response = await appointmentmanager.printAppointments(appointment, currentUser);
            HttpContext.Response.ContentType = "application/pdf";
            FileContentResult result = new FileContentResult(response, "application/pdf")
            {
                FileDownloadName = "report.pdf"
            };
            return result;
        }

        // POST api/appointment
        [HttpPost("createAppointment")]
        public async Task<IActionResult> createAppointmentAsync([FromBody] dynamic appointmentData)
        {
            if (appointmentData == null)
                return NotFound();
            AppointmentData appointment = JsonConvert.DeserializeObject<AppointmentData>(appointmentData.appointmentData.ToString());

            string currentUser = getLoggedInUser(userRoleRequired: false);
            Appointment app = await appointmentmanager.createAppointmentAsync(appointment, currentUser);

            if (app._id != null)
                return CreatedAtRoute("getAppointmentById", new { id = app._id }, app);
            else
                return null;
        }

        // POST api/appointment/5
        [HttpPost("updateAppointment")]
        public async Task<IActionResult> updateAppointmentAsync([FromBody]dynamic appointmentData)
        {
            try
            {
                if (appointmentData == null)
                    return NotFound();
                AppointmentData appointment = JsonConvert.DeserializeObject<AppointmentData>(appointmentData.appointmentData.ToString());

                long matchedCount = await appointmentmanager.updateAppointmentAsync(appointment);

                if (matchedCount > 0)
                    return new NoContentResult();
            }
            catch (Exception ex)
            {
                return new ContentResult() { Content = ex.Message, StatusCode = (int)HttpStatusCode.BadRequest };
            }
            return NotFound();
        }

        // DELETE api/appointment/5
        [HttpDelete("deleteAppointment")]
        public async Task<IActionResult> deleteAppointmentAsync()
        {
            try
            {
                string role = getLoggedInUser();

                if (role.Contains("vtsTestUsers"))
                {
                    long deleteCount = await appointmentmanager.deleteAppointmentAsync();
                    if (deleteCount > 0)
                        return new NoContentResult();
                }

            }
            catch (Exception ex)
            {
                return new ContentResult() { Content = ex.Message, StatusCode = (int)HttpStatusCode.BadRequest };
            }
            return NotFound();
        }

        [HttpPost("checkIn")]
        public async Task<IActionResult> checkInAsync([FromBody]dynamic appdata)
        {
            try

            {
                if (appdata == null)
                    return NotFound();
                string appid = appdata.appointmentid;
                string asset = appdata.AssetContent;
                JArray visitorData = appdata.newVisitors;
                string[] visitorsattended = visitorData.ToObject<string[]>();
                long matchedCount = await appointmentmanager.checkInAsync(appid, asset, visitorsattended);

                if (matchedCount > 0)
                    return new NoContentResult();
            }
            catch (Exception ex)
            {
                return new ContentResult() { Content = ex.Message, StatusCode = (int)HttpStatusCode.BadRequest };
            }
            return NotFound();
        }

        [HttpPost("checkOut")]
        public async Task<IActionResult> checkOutAsync([FromBody]dynamic appdata)
        {
            try
            {
                if (appdata == null)
                    return NotFound();

                string appid = appdata.appointmentid;
                string asset = await appointmentmanager.checkOutAsync(appid);
                return new ContentResult() { Content = asset };
            }
            catch (Exception ex)
            {
                return new ContentResult() { Content = ex.Message, StatusCode = (int)HttpStatusCode.BadRequest };
            }
        }

        [HttpPost("updateAssets")]
        public async Task<IActionResult> updateAssets([FromBody]dynamic appdata)
        {
            try

            {
                if (appdata == null)
                    return NotFound();

                string appid = appdata.appointmentid;
                string asset = appdata.AssetContent;
                long matchedCount = await appointmentmanager.updateAssets(appid, asset);

                if (matchedCount > 0)
                    return new NoContentResult();
            }
            catch (Exception ex)
            {
                return new ContentResult() { Content = ex.Message, StatusCode = (int)HttpStatusCode.BadRequest };
            }
            return NotFound();
        }

        #region private method
        private string getUserRole(string name)
        {
            string role = "associate";
            using (var context = new PrincipalContext(ContextType.Domain, "senecaglobal.net"))
            {
                using (var searcher = new PrincipalSearcher(new UserPrincipal(context)))
                {
                    foreach (var result in searcher.FindAll())
                    {
                        DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;
                        if (!de.Path.Contains(_organizationalUnit))
                            continue;
                        string user = (string)de.Properties[_samAccountName].Value;

                        if (name.Equals(user))
                        {
                            Console.WriteLine("User exists");
                            object[] members = (object[])de.Properties[_memberOf].Value;

                            var security = members.Where(e => e.ToString().Contains(_vtsSecurity)).FirstOrDefault();
                            var admin = members.Where(e => e.ToString().Contains(_vtsAdmin)).FirstOrDefault();
                            var testUser = members.Where(e => e.ToString().Contains(_vtsTestUsers)).FirstOrDefault();
                            if (admin != null)
                            {
                                role = _vtsAdmin;
                                break;
                            }

                            else if (security != null)
                            {
                                role = _vtsSecurity;
                                break;
                            }

                            else if (testUser != null)
                            {
                                role = _vtsTestUsers;
                                break;
                            }

                        }
                    }
                }
            }
            return role;
        }

        #endregion
    }
}
