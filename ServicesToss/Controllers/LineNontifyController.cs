using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web.Http;
using Newtonsoft.Json;
using ServicesToss.Models;
using System.Text;
using System.Data;

namespace ServicesToss.Controllers
{
    public class LineNontifyController : ApiController
    {


        // POST: LineNontify
        [HttpPost, Route("api/LineNotify/SendMessage")]
        public IHttpActionResult SendNotify(RequestLine Request)
        {
            //string token = "9IBnp37LVHj0a6W5HLq2dF7sqIjGyEVn2DQtpQq7wYv";
            if (Request.apikey != "c39fd178d64ff373ef1c8faeeec63740")
            {
                return Ok(new
                {
                    Status = "Fail",
                    Message = "API KEY ไม่ถูกต้อง",
                    Data = "",
                });
            }
            try
            {
                var obj = Request;
                if (string.IsNullOrEmpty(Request.Type)) //// ส่งทีละกลุ่ม
                {
                    SendNotificaion(Request);
                }
                else { /// ส่งทุกกลุ่ม
                    var dt = GetDataAllGroup();
                    foreach (DataRow row in dt.Rows)
                    {
                        string DepartID = row.ItemArray[0].ToString();
                        string DepartName = row.ItemArray[1].ToString();
                        string Token = row.ItemArray[2].ToString();
                        obj.token = Token;
                        try {
                            SendNotificaion(obj);
                        }
                        catch (Exception ex) {

                        }
                    }
                }

                return Ok(new
                {
                    Status = "SUCCESS",
                    Message = "",
                    Data = "",
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Status = "ERROR",
                    Message = ex.ToString(),
                    Data = "",
                });
            }

        }


        // GET: test
        [HttpGet/*, Route("api/SmartCare")*/]
        public IHttpActionResult Index()
        {
            return Ok(new
            {
                status = "200",
                message = "SendNotificaion hello world",
            });
        }

        // POST: LineNontify
        [HttpPost, Route("api/LineNotify/SendLineNotify")]
        public void SendLineNotify(RequestLine Request) {
            var request = (HttpWebRequest)WebRequest.Create("https://notify-api.line.me/api/notify");
            var postData = string.Format("message={0}", Request.msg);
            if (Request.stickerPackageID > 0 && Request.stickerID > 0)
            {
                var stickerPackageId = string.Format("stickerPackageId={0}", Request.stickerPackageID);
                var stickerId = string.Format("stickerId={0}", Request.stickerID);
                postData += "&" + stickerPackageId.ToString() + "&" + stickerId.ToString();
            }
            if (Request.pictureUrl != "")
            {
                var imageThumbnail = string.Format("imageThumbnail={0}", Request.pictureUrl);
                var imageFullsize = string.Format("imageFullsize={0}", Request.pictureUrl);
                postData += "&" + imageThumbnail.ToString() + "&" + imageFullsize.ToString();
            }
            var data = Encoding.UTF8.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            request.Headers.Add("Authorization", "Bearer " + Request.token);

            using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
            var response = (HttpWebResponse)request.GetResponse();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

        }

        public DataTable GetDataAllGroup() {
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            string sql = @"SELECT l.DepartID,vt.DepartName,l.LineToken
                FROM TSR_ONLINE_MARKETING.dbo.Problem_Depart_LineToken l
                LEFT JOIN TSR_ONLINE_MARKETING.dbo.V_TicketDepart vt ON l.DepartID = vt.DepartId
                WHERE l.TokenStatus = 1";
            DataTable dt = new DataTable();
            dt = conn.ExecuteDataTable(DbConnectionString, sql, null);
            return dt;
        }










        public void SendNotificaion(RequestLine Request)
        {
            var request = (HttpWebRequest)WebRequest.Create("https://notify-api.line.me/api/notify");
            var postData = string.Format("message={0}", Request.msg);
            if (Request.stickerPackageID > 0 && Request.stickerID > 0)
            {
                var stickerPackageId = string.Format("stickerPackageId={0}", Request.stickerPackageID);
                var stickerId = string.Format("stickerId={0}", Request.stickerID);
                postData += "&" + stickerPackageId.ToString() + "&" + stickerId.ToString();
            }
            if (Request.pictureUrl != "")
            {
                var imageThumbnail = string.Format("imageThumbnail={0}", Request.pictureUrl);
                var imageFullsize = string.Format("imageFullsize={0}", Request.pictureUrl);
                postData += "&" + imageThumbnail.ToString() + "&" + imageFullsize.ToString();
            }
            var data = Encoding.UTF8.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;
            request.Headers.Add("Authorization", "Bearer " + Request.token);

            using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);
            var response = (HttpWebResponse)request.GetResponse();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

        }







    }

    public class RequestLine {
        public string apikey { get; set; }
        public string token { get; set; }
        public string msg { get; set; }
        public int stickerPackageID { get; set; }
        public int stickerID { get; set; }
        public string pictureUrl { get; set; }
        public string Type { get; set; }
    }
}