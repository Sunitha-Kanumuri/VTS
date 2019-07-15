using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SenecaGlobal.VTS.Types;
using SenecaGlobal.VTS.Types.Interfaces;
using MongoDB.Driver;
using SenecaGlobal.VTS.Library.Extensions;
using MongoDB.Bson;
using Newtonsoft.Json;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace SenecaGlobal.VTS.Library
{
    public class AppointmentManager : IAppointmentManager
    {
        private IConfiguration configuration;
        public AppointmentManager(IConfiguration config)
        {
            this.configuration = config;
        }

        public async Task<ScrollList<Appointment>> getAppointmentsAsync(string currentUser, string searchKey = null, string status = null, string startDate = null, string endDate = null, string scope = null, string appointmentTime = null, string sortField = null, int? pageNo = null, int? pageSize = null)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();
            FilterDefinition<Appointment> searchFilter = null;

            if (!string.IsNullOrWhiteSpace(scope) && "mine".Equals(scope, StringComparison.OrdinalIgnoreCase) || "myhistory".Equals(scope, StringComparison.OrdinalIgnoreCase))
            {
                FilterDefinition<Appointment> scopeFilter = Builders<Appointment>.Filter.Where(x => x.whomToMeet == currentUser);
                if (searchFilter == null) searchFilter = scopeFilter;
                else searchFilter = searchFilter & scopeFilter;
            }

            if (!string.IsNullOrWhiteSpace(scope))
            {
                if ("myhistory".Equals(scope, StringComparison.OrdinalIgnoreCase))
                {
                    FilterDefinition<Appointment> scopeFilter = Builders<Appointment>.Filter.Where(x => x.status.ToLower() != StatusCodes.Upcoming.ToString().ToLower());
                    if (searchFilter == null) searchFilter = scopeFilter;
                    else searchFilter = searchFilter & scopeFilter;
                }
                else if ("security".Equals(scope, StringComparison.OrdinalIgnoreCase))
                {
                    FilterDefinition<Appointment> scopeFilter = Builders<Appointment>.Filter.Where(x => x.status.ToLower() == StatusCodes.Upcoming.ToString().ToLower());
                    scopeFilter = scopeFilter | (Builders<Appointment>.Filter.Where(x => x.status.ToLower() == StatusCodes.Active.ToString().ToLower()));
                    FilterDefinition<Appointment> currentDayFilter = Builders<Appointment>.Filter.Where(x => x.appointmentTime > DateTime.Today && x.appointmentTime < DateTime.Today.AddDays(1));
                    if (searchFilter == null) searchFilter = scopeFilter;
                    else searchFilter = searchFilter & scopeFilter;

                    searchFilter = searchFilter & currentDayFilter;
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                FilterDefinition<Appointment> statusFilter = Builders<Appointment>.Filter.Where(x => x.status.ToLower() == status.ToLower());
                if (searchFilter == null) searchFilter = statusFilter;
                else searchFilter = searchFilter & statusFilter;
            }

            //Date Filter
            if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
            {
                DateTime fromDate = DateTime.ParseExact(startDate, "dd-MM-yyyy", null);
                DateTime toDate = DateTime.ParseExact(endDate, "dd-MM-yyyy", null);
                FilterDefinition<Appointment> dateFilter = Builders<Appointment>.Filter.And(
                                 Builders<Appointment>.Filter.Gte("appointmentTime", fromDate.startOfDay()),
                                 Builders<Appointment>.Filter.Lte("appointmentTime", toDate.endOfDay()));
                if (searchFilter == null) searchFilter = dateFilter;
                else searchFilter = searchFilter & dateFilter;
            }

            else if (!string.IsNullOrWhiteSpace(scope) && "mine".Equals(scope, StringComparison.OrdinalIgnoreCase)
              && !string.IsNullOrWhiteSpace(status) && "upcoming".Equals(status, StringComparison.OrdinalIgnoreCase))
            {
                FilterDefinition<Appointment> dateFilter = Builders<Appointment>.Filter.Gte("appointmentTime", DateTime.Now.startOfDay());
                if (searchFilter == null) searchFilter = dateFilter;
                else searchFilter = searchFilter & dateFilter;
            }

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                FilterDefinition<Appointment> searchKeyFilter = Builders<Appointment>.Filter.Or(
                               Builders<Appointment>.Filter.Where(x => x.visitorName.Any(z => z.ToLower().Contains(searchKey.ToLower()))),
                               Builders<Appointment>.Filter.Where(x => x.whomToMeet.ToLower().Contains(searchKey.ToLower())));
                if (searchFilter == null) searchFilter = searchKeyFilter;
                else searchFilter = searchFilter & searchKeyFilter;
            }
            SortDefinition<Appointment> sort = null;

            if (!string.IsNullOrWhiteSpace(sortField))
            {
                string[] fields = sortField.Split('|');
                if ("asc".Equals(fields[1], StringComparison.OrdinalIgnoreCase))
                    sort = Builders<Appointment>.Sort.Ascending(fields[0]);
                else if ("desc".Equals(fields[1], StringComparison.OrdinalIgnoreCase))
                    sort = Builders<Appointment>.Sort.Descending(fields[0]);
            }
            //To display required data
            var projections = Builders<Appointment>.Projection.Expression(x => new Appointment
            {
                _id = x._id,
                typeOfVisitor = x.typeOfVisitor,
                visitorName = x.visitorName,
                organizationName = x.organizationName,
                appointmentTime = x.appointmentTime,
                whomToMeet = x.whomToMeet,
                phoneNumber = x.phoneNumber,
                instructions = x.instructions,
                status = x.status,
                inTime = x.inTime,
                outTime = x.outTime,
                assets = x.assets,
                attendedVisitors= x.attendedVisitors
            });

            if (searchFilter == null) searchFilter = new BsonDocument();

            IFindFluent<Appointment, Appointment> query = collection.Find(searchFilter).Project(projections);

            if (sort != null) query = query.Sort(sort);

            List<Appointment> appointments = null;
            ScrollList<Appointment> appointmentInfo = new ScrollList<Appointment>();

            if (pageNo.HasValue && pageSize.HasValue)
            {
                int skip = (pageNo.Value - 1) * pageSize.Value;
                long rowCount = collection.Find(searchFilter).Count();

                if (rowCount == 0) appointmentInfo.dataEnd = appointmentInfo.dataStart = true;

                if (pageNo * pageSize.Value >= rowCount)
                {
                    pageSize = Convert.ToInt32(rowCount) - skip;
                    appointmentInfo.dataEnd = true;
                }

                if (skip >= 0 && pageSize >= 0) query = query.Skip(skip).Limit(pageSize);
            }
            appointments = await query.ToListAsync();
            appointmentInfo.appointments = appointments;
            return appointmentInfo;
        }

        public async Task<Appointment> createAppointmentAsync(AppointmentData appointment, string currentUser)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collec = GetMongoCollection();

            long count = await checkValidAppointmentAsync(appointment.visitorName, appointment.whomToMeet, DateTime.Parse(appointment.appointmentTime));

            if (count == 0)
            {
                var app = new Appointment()
                {
                    typeOfVisitor = appointment.typeOfVisitor,
                    visitorName = appointment.visitorName,
                    organizationName = appointment.organizationName,
                    whomToMeet = appointment.whomToMeet,
                    phoneNumber = appointment.phoneNumber,
                    instructions = appointment.instructions,
                    appointmentTime = DateTime.Parse(appointment.appointmentTime),
                    status = "Upcoming",
                    createdBy = currentUser,
                    createdDate = DateTime.Now

                };

                await collec.InsertOneAsync(app);
                return app;
            }
            else
            {
                throw new Exception("This appointment entry already exists");
            }

        }

        public async Task<long> updateAppointmentAsync(AppointmentData appointment)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();
            UpdateResult result;

            try
            {
                UpdateDefinition<Appointment> query;
                var filter = Builders<Appointment>.Filter.Eq("_id", ObjectId.Parse(appointment._id));

                if (!string.IsNullOrWhiteSpace(appointment.status) && "Cancelled".Equals(appointment.status, StringComparison.OrdinalIgnoreCase))
                {
                    query = Builders<Appointment>.Update
                        .Set("status", appointment.status);
                }
                else
                {
                    query = Builders<Appointment>.Update
                        .Set("typeOfVisitor", appointment.typeOfVisitor)
                        .Set("visitorName", appointment.visitorName)
                        .Set("whomToMeet", appointment.whomToMeet)
                        .Set("phoneNumber", appointment.phoneNumber)
                        .Set("instructions", appointment.instructions)
                        .Set("organizationName", appointment.organizationName)
                        .Set("appointmentTime", DateTime.Parse(appointment.appointmentTime));
                }

                result = await collection.UpdateOneAsync(filter, query);
                return result.MatchedCount;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(JsonConvert.SerializeObject(
                  new
                  {
                      error = "Failed to Update an Appointment. Data update error.",
                      error_code = ErrorCodes.DataUpdateError,
                      block_name = System.Reflection.MethodBase.GetCurrentMethod().Name
                  }));
            }
        }

        public async Task<long> deleteAppointmentAsync()
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();
            DeleteResult result;

            FilterDefinition<Appointment> filter = new BsonDocument();
            result = await collection.DeleteManyAsync(filter);
            return result.DeletedCount;

        }

        public async Task<byte[]> printAppointments(AppointmentData appointment, string currentUser)
        {
            ScrollList<Appointment> scrollList = await getAppointmentsAsync(currentUser, appointment.searchKey, appointment.status, appointment.startDate, appointment.endDate, appointment.scope, appointment.appointmentTime, appointment.sortField, appointment.pageNo, appointment.pageSize);
            byte[] data = GeneratePDFReport(scrollList.appointments, appointment.startDate, appointment.endDate);
            return data;
        }

        public async Task<long> checkInAsync(string Id, string assets,string[] attendedvisitors)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();
            UpdateResult result;
            try
            {
                string checkInTime = DateTime.Now.ToString("hh:mm tt");
                UpdateDefinition<Appointment> query = null;
                var filter = Builders<Appointment>.Filter.Eq("_id", ObjectId.Parse(Id));

                query = Builders<Appointment>.Update
                    .Set("status", StatusCodes.Active.ToString())
                    .Set("inTime", checkInTime)
                    .Set("assets", assets)
                .Set("attendedVisitors", attendedvisitors);

                result = await collection.UpdateOneAsync(filter, query);
                return result.MatchedCount;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(JsonConvert.SerializeObject(
                  new
                  {
                      error = "Failed to Check-In an Appointment.",
                      error_code = ErrorCodes.DataUpdateError,
                      block_name = System.Reflection.MethodBase.GetCurrentMethod().Name
                  }));
            }
        }

        public async Task<string> checkOutAsync(string Id)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();
            UpdateResult result;
            try
            {
                string checkOutTime = DateTime.Now.ToString("hh:mm tt");
                UpdateDefinition<Appointment> query = null;
                var filter = Builders<Appointment>.Filter.Eq("_id", ObjectId.Parse(Id));

                query = Builders<Appointment>.Update
                    .Set("status", StatusCodes.Visited.ToString())
                    .Set("outTime", checkOutTime);

                result = await collection.UpdateOneAsync(filter, query);
                if (result.MatchedCount > 0)
                {
                    FilterDefinition<Appointment> searchById = Builders<Appointment>.Filter.Where(x => x._id.Equals(ObjectId.Parse(Id)));
                    IFindFluent<Appointment, Appointment> query1 = collection.Find(searchById);
                    List<Appointment> appointments = null;
                    appointments = await query1.ToListAsync<Appointment>();
                    return appointments[0].assets;
                }
                return null;

            }
            catch (Exception ex)
            {
                throw new ApplicationException(JsonConvert.SerializeObject(
                  new
                  {
                      error = "Failed to Check-Out an Appointment.",
                      error_code = ErrorCodes.DataUpdateError,
                      block_name = System.Reflection.MethodBase.GetCurrentMethod().Name
                  }));
            }
        }

        public async Task<List<string>> suggestVisitorName(string searchString)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();

            FilterDefinition<Appointment> searchKeyFilter = Builders<Appointment>.Filter.Where(x => x.visitorName.Any(z => z.ToLower().StartsWith(searchString.ToLower())));
            var projections = Builders<Appointment>.Projection.Expression(x => new Appointment
            {
                visitorName = x.visitorName.Where(y => y.ToLower().StartsWith(searchString.ToLower())).ToArray()
            });

            List<Appointment> appointments = null;
            IFindFluent<Appointment, Appointment> query = collection.Find(searchKeyFilter).Project(projections);
            appointments = await query.ToListAsync<Appointment>();
            return appointments.Select(x => x.visitorName.FirstOrDefault()).Distinct().ToList();
        }

        public async Task<long> updateAssets(string Id, string assets)
        {
            IMongoDatabase database = this.GetDatabase();
            IMongoCollection<Appointment> collection = GetMongoCollection();
            UpdateResult result;
            try
            {
                UpdateDefinition<Appointment> query = null;
                var filter = Builders<Appointment>.Filter.Eq("_id", ObjectId.Parse(Id));

                query = Builders<Appointment>.Update
                    .Set("assets", assets);

                result = await collection.UpdateOneAsync(filter, query);
                return result.MatchedCount;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(JsonConvert.SerializeObject(
                  new
                  {
                      error = "Failed to Update an Asset.",
                      error_code = ErrorCodes.DataUpdateError,
                      block_name = System.Reflection.MethodBase.GetCurrentMethod().Name
                  }));
            }
        }


        #region private methods
        private IMongoDatabase GetDatabase()
        {
            MongoInstance.Configuration = this.configuration;
            IMongoDatabase mongoDatabase = MongoInstance.Instance;
            return mongoDatabase;
        }
        private IMongoCollection<Appointment> GetMongoCollection()
        {
            IMongoDatabase database = this.GetDatabase();
            var collection = database.GetCollection<Appointment>("Appointments");
            return collection;
        }
        private async Task<long> checkValidAppointmentAsync(string[] visitorName, string whomToMeet, DateTime appointmentTime)
        {
            IMongoCollection<Appointment> collection = GetMongoCollection();
            long count = 0;

            var searchFilter = Builders<Appointment>.Filter.And(
                               Builders<Appointment>.Filter.Where(x => x.visitorName.Any(i => visitorName.Contains(i))),
                               Builders<Appointment>.Filter.Where(x => x.whomToMeet.ToLower() == whomToMeet.ToLower()),
                               Builders<Appointment>.Filter.Where(x => x.appointmentTime == appointmentTime));

            count = await collection.Find(searchFilter).CountAsync();

            return count;
        }
        private byte[] GeneratePDFReport(List<Appointment> appointments, string startDate, string endDate)
        {
            Stream stream;
            Paragraph para = null;
            Phrase pharse = null;
            PdfPTable table = null;
            PdfPCell cell = null;
            Document document = new Document(PageSize.A4_LANDSCAPE, 30, 30, 30, 130); // PageSize.A4, left, right, top , bottom
            Font contentFont = FontFactory.GetFont(FontFactory.HELVETICA, 11f, BaseColor.BLACK);
            Font contentFontBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11f, BaseColor.BLACK);
            byte[] bytes;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PdfWriter.GetInstance(document, memoryStream);
                document.Open();
                table = PreparePDFTable(document, para, contentFontBold, contentFont, table, cell, pharse, appointments, startDate, endDate);
                document.Add(table);
                document.Close();
                stream = memoryStream;
                bytes = memoryStream.ToArray();
                memoryStream.Close();

            }
            return bytes;
        }
        private PdfPTable PreparePDFTable(Document document, Paragraph para, Font contentFontBold, Font contentFont, PdfPTable table, PdfPCell cell, Phrase pharse, List<Appointment> appointments, string startDate, string endDate)
        {
            if (string.IsNullOrEmpty(startDate) && string.IsNullOrEmpty(endDate))
            {
                para = ParagraphContent("Appointment History Report ", contentFontBold, spacingAfter: 20);
            }
            else if (startDate != null && endDate == null)
            {
                var dt = (DateTime.Now).ToString();
                endDate = dt.Split(' ')[0];
                para = ParagraphContent("Appointment History Report from " + startDate + " " + " to " + " " + endDate, contentFontBold, spacingAfter: 20);
            }
            else
            {
                para = ParagraphContent("Appointment History Report from " + startDate + " " + " to " + " " + endDate, contentFontBold, spacingAfter: 20);
            }
            document.Add(para);

            table = new PdfPTable(8);
            table.TotalWidth = 500;
            table.LockedWidth = true;

            float[] widths = new float[] { 50f, 50f, 60f, 50f, 60f, 50f, 50f, 50f };
            table.SetWidths(widths);

            pharse = new Phrase();
            pharse.Add(new Chunk("Name", contentFontBold));
            cell = PhraseCell(pharse);
            table.AddCell(cell);

            pharse = new Phrase();
            pharse.Add(new Chunk("Visitor Type", contentFontBold));
            cell = PhraseCell(pharse);
            table.AddCell(cell);

            pharse = new Phrase();
            pharse.Add(new Chunk("Organization", contentFontBold));
            cell = PhraseCell(pharse);
            table.AddCell(cell);

            pharse = new Phrase();
            pharse.Add(new Chunk("Whom To Meet ", contentFontBold));
            cell = PhraseCell(pharse);
            table.AddCell(cell);

            cell = PhraseCell(new Phrase("Appointment Time", contentFontBold), paddingRight: 0f, paddingLeft: 2f);
            table.AddCell(cell);

            cell = PhraseCell(new Phrase("In-Time", contentFontBold));
            table.AddCell(cell);

            cell = PhraseCell(new Phrase("Out-Time", contentFontBold));
            table.AddCell(cell);

            cell = PhraseCell(new Phrase("Status", contentFontBold));
            table.AddCell(cell);



            foreach (var appointment in appointments)
            {
                cell = PhraseCell(new Phrase(String.Join(",", appointment.visitorName), contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.typeOfVisitor, contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.organizationName, contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.whomToMeet, contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.appointmentTime.ToString("dd/MM/yyyy hh:mm tt"), contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.inTime, contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.outTime, contentFont));
                table.AddCell(cell);
                cell = PhraseCell(new Phrase(appointment.status, contentFont));
                table.AddCell(cell);
            }

            return table;
        }
        private static Paragraph ParagraphContent(string content, Font contentFont, int align = Element.ALIGN_JUSTIFIED, int spacingBefore = 10, int spacingAfter = 0)
        {
            Paragraph paragraph = new Paragraph();
            paragraph.SpacingBefore = spacingBefore;
            paragraph.SpacingAfter = spacingAfter;
            paragraph.Font = contentFont;
            paragraph.Alignment = align;
            paragraph.Add(content);
            return paragraph;
        }
        private static PdfPCell PhraseCell(Phrase phrase, int align = Element.ALIGN_LEFT, float paddingLeft = 2f, float paddingRight = 2f)
        {
            PdfPCell cell = new PdfPCell(phrase);
            cell.VerticalAlignment = Element.ALIGN_TOP;
            cell.HorizontalAlignment = align;
            cell.PaddingLeft = paddingLeft;
            cell.PaddingRight = paddingRight;
            cell.PaddingBottom = 7f;
            cell.PaddingTop = 7f;
            return cell;
        }
        #endregion
    }
}
