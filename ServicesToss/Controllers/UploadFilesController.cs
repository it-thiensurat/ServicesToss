using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using ServicesToss.Models;
using WebSocketSharp;
namespace ServicesToss.Controllers
{
    public class UploadFilesController : ApiController
    {

        // GET api/<controller>/5
        [HttpGet, Route("UploadFiles/RequestExample")]
        public IHttpActionResult Example()
        {
            return Ok(new
            {
                Message = "Hello World",
            });
        }




        /// API UploadFiles 
        [HttpPost, Route("UploadFiles/SaveFile")]
        public IHttpActionResult SaveFile(RequestSaveFiles request)
        {
            if (request == null) {
                return Ok(new
                {
                    Status = 500,
                    message = "parameter is wrong!",
                    url = "",
                });
            }
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON result = new ReturnJSON();
            try
            {
                string Host = "ftp.thiensurat.com";
                string url = "";
                int Port = 21;
                string UserName = "", Password = "";
                if (request.ApplicationType.ToUpper() == "MOBILE")
                {
                    url = "http://thiensurat.com/fileshare02";
                    UserName = "fileshare02@thiensurat.com";
                    Password = "RUHWSlouy7";
                } else if (request.ApplicationType.ToUpper() == "WEB") {
                    url = "http://thiensurat.com/fileshare01";
                    UserName = "fileshare01@thiensurat.com";
                    Password = "CX8Q2Z7wO";
                }
                string FullPath = DateTime.Now.Year + "/" + DateTime.Now.Month.ToString("D2") + "/";


                string bhpath = "ftp://" + Host + "/" + request.ProjectName;/// + "/" + DateTime.Now.Year + "/" + DateTime.Now.Month.ToString("D2") + "/";
                ImgeCheckDirectory(bhpath, UserName, Password); /// check directory projectname

                bhpath = bhpath + "/" + DateTime.Now.Year;
                ImgeCheckDirectory(bhpath, UserName, Password); /// check directory year

                bhpath = bhpath + "/" + DateTime.Now.Month.ToString("D2");
                ImgeCheckDirectory(bhpath, UserName, Password); /// check directory month

                bhpath = bhpath + "/" + request.FileName + "." + request.FileType.ToLower();


                /// write files
                FtpWebRequest ftpReq = (FtpWebRequest)WebRequest.Create(bhpath);
                ftpReq.UseBinary = true;
                ftpReq.Method = WebRequestMethods.Ftp.UploadFile;
                ftpReq.Credentials = new NetworkCredential(UserName, Password);
                byte[] b = Convert.FromBase64String(request.Base64);
                ftpReq.ContentLength = b.Length;
                using (Stream s = ftpReq.GetRequestStream())
                {
                    s.Write(b, 0, b.Length);
                }
                FtpWebResponse ftpResp = (FtpWebResponse)ftpReq.GetResponse();
                string StatusDescription = "";
                if (ftpResp != null)
                {
                    StatusDescription = ftpResp.StatusDescription;
                }

                //// save log
                url = url + "/" + request.ProjectName + "/" + DateTime.Now.Year + "/" + DateTime.Now.Month.ToString("D2") + "/" + request.FileName + "." + request.FileType.ToLower();
                conn.InsertData(DbConnectionString, conn.sqlLogServiceToss, new
                {
                    ProjectName = "UploadFiles",
                    ControllerName = "UploadFiles",
                    FunctionName = "SaveFile",
                    JsonRequestData = new JavaScriptSerializer().Serialize(request)
                    ,
                    JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        status = "success",
                        message = StatusDescription,
                        path = url,
                        size = b.Length,
                    })
                });
                return Ok(new
                {
                    status = "success",
                    message = StatusDescription,
                    path = url,
                    size = b.Length,
                });
            }
            catch (Exception ex)
            {
                conn.InsertData(DbConnectionString, conn.sqlLogServiceToss, new
                {
                    ProjectName = "UploadFiles",
                    ControllerName = "UploadFiles",
                    FunctionName = "SaveFile",
                    JsonRequestData = new JavaScriptSerializer().Serialize(request),
                    JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        Status = "error",
                        message = ex.Message.ToString(),
                        path = "",
                        size = 0,
                    })
                });
                return Ok(new
                {
                    Status = "error",
                    message = ex.Message.ToString(),
                    path = "",
                    size = 0,
                });
            }

        }



        public bool ImgeCheckDirectory(string pathname,string UserName,string  Password)
        {
            //string bhpath = "ftp://tsrdev@192.168.110.133:22/2015/08/";
            try
            {
                //create the directory
                var client = WebRequest.Create(pathname);
                client.Credentials = new NetworkCredential(UserName, Password);
                client.Method = WebRequestMethods.Ftp.MakeDirectory;
                FtpWebResponse response = (FtpWebResponse)client.GetResponse();
                response.Close();
                return true;
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    response.Close();
                    return true;
                }
                else
                {
                    response.Close();
                    return false;
                }
            }
        }


    }





    public class RequestSaveFiles {
        public string ApplicationType { get; set; } ///// MOBILE , WEB
        public string ProjectName { get; set; } 
        public string FileName { get; set; }
        public string Base64 { get; set; }
        public string FileType { get; set; }
        public string IPAddess { get; set; }
        public string ComputerName { get; set; }
        public string ByUser { get; set; }
    }
}