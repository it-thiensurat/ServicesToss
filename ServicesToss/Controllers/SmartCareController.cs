using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Script.Serialization;
using Dapper;
using Newtonsoft.Json;
using ServicesToss.Models;
using WebSocketSharp;

namespace ServicesToss.Controllers
{
    public class SmartCareController : ApiController
    {
        // GET: SmartCare
        [HttpGet, Route("api/SmartCare")]
        public IHttpActionResult Index()
        {
            return Ok(new
            {
                status = "200",
                message = "SmartCare hello world",
            });
        }


        // POST: OpenTicket
        [HttpPost, Route("api/SmartCare/OpenTicket")]
        public IHttpActionResult OpenTicketFromSmartCare(RequestTicket request)
        {
            //return Ok(new
            //{
            //    STATUS = 200,
            //    MESSAGE = "Successful",
            //    InformID = request,
            //});
            //string DbConnectionString = WebConfigurationManager.ConnectionStrings[Properties.Settings.Default.ConnectionType == "UAT" ? "TSR-SQL-UAT" : "TSR-SQL-02"].ConnectionString;
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-UAT"].ConnectionString;
            try
            {
                //string[] ArrImages = request.Images.Replace("[", "").Replace("]", "").Split(',');

                /* Create Object Instance */
                ftp ftpClient = new ftp();
                ConnectDB connectdb = new ConnectDB();
                string sqlInformID = @"SELECT [TSR_ONLINE_MARKETING].[dbo].[GenProblemID]() AS InformID";
                DataTable dt = new DataTable();
                dt = connectdb.ExecuteDataTable(DbConnectionString, sqlInformID, null);
                if (dt.Rows.Count == 0) {
                    return Ok(new
                    {
                        status = "SUCCESS",
                        message = "Can't create Inform Number!",
                        InformID = "",
                    });
                }
                string InformID = dt.Rows[0]["InformID"].ToString();
                string WorkCode = "22";
                string UserCode = "SmartCare";
                int ProblemID = 1102;
                using (var conn = new SqlConnection(DbConnectionString))
                {
                    conn.Open();
                    // create the transaction
                    // You could use `var` instead of `SqlTransaction`
                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                        try
                        {
                            string sql_Insert_Inform_Master = @"
                                INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Inform_Master (
                                    InformID,
                                    InfromDateTime,
                                    Contno,
                                    CustomerPhone,
                                    InformEmpID,
                                    InformDepartID,
                                    WorkCode,
                                    date_create,
                                    date_modify,
                                    user_code,
                                    ipaddress,
                                    computername,
                                    InformStatus,
                                    DataChannel
                                ) VALUES (
                                    @InformID,
                                    GETDATE(),
                                    @Contno,
                                    @CustomerPhone,
                                    @InformEmpID,
                                    @InformDepartID,
                                    @WorkCode,
                                    GETDATE(),
                                    GETDATE(),
                                    @user_code,
                                    @ipaddress,
                                    @computername,
                                    1,
                                    '04'
                                )
                            ";
                            conn.Execute(sql_Insert_Inform_Master, new
                            {
                                InformID = InformID,
                                Contno = request.Contno,
                                CustomerPhone = request.Tel,
                                InformEmpID = UserCode,
                                InformDepartID = UserCode,
                                WorkCode = WorkCode,
                                user_code = UserCode,
                                ipaddress = request.IpAddress,
                                computername = request.DeviceName,
                            }, tran);

                            string sql_Insert_Inform_Detail = @"
                                INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Inform_Details (
                                    InformID,
                                    ProblemID,
                                    ProblemTopic,
                                    ProblemDetail,
                                    ProblemStatus 
                                ) VALUES (
                                    @InformID,
                                    @ProblemID,
                                    @ProblemTopic,
                                    @ProblemDetail,
                                    1
                                )
                            ";
                            conn.Execute(sql_Insert_Inform_Detail, new
                            {
                                InformID = InformID,
                                ProblemID = ProblemID,
                                ProblemTopic = request.ProductType,
                                ProblemDetail = request.ProblemDetails,
                            }, tran);
                            
                            int i = 1;

                            foreach (ObjectImage base64 in request.Images)
                            {
                                if (base64.data != null)
                                {
                                    string filename = i.ToString() + "_" + DateTime.Now.ToString("MMddyyyy_HHmmss");
                                    int size = ftpClient.GetOriginalLengthInBytes(base64.data);
                                    var result = ftpClient.FtpUpFile(base64.data, filename, DbConnectionString);
                                    if (result.result)
                                    {
                                        string sql_Insert_Inform_Detail_Images = @"INSERT INTO [TSR_ONLINE_MARKETING].[dbo].Problem_Inform_Details_Images (
                                        InformID,
                                        ProblemID,
                                        ImageItem,
                                        ImageUrl,
                                        ImageName,
                                        ImageSize,
                                        ImageType
                                    )VALUES
                                    (
                                        @InformID,
                                        @ProblemID,
                                        @ImageItem,
                                        @ImageUrl,
                                        @ImageName,
                                        @ImageSize,
                                        @ImageType
                                    )";
                                        conn.Execute(sql_Insert_Inform_Detail_Images, new
                                        {
                                            InformID = InformID,
                                            ProblemID = ProblemID,
                                            ImageItem = i,
                                            ImageUrl = result.url.ToString(),
                                            ImageName = filename,
                                            ImageSize = size,
                                            ImageType = ".jpeg",
                                        }, tran);

                                    }
                                    i++;
                                }
                            }

                            string sql_Depart = @"SELECT pdd.[DepartID],
                            [TSR_ONLINE_MARKETING].[dbo].dbo.ConvertProblemIDToProblemFullName(pdm.ProblemID) AS ProblemName,
                            ISNULL(lt.LineToken,'') AS LineToken,
                            pm.SLA,
                            dbo.fn_ConvertDateSqlToDateThai(DATEADD(HOUR, pm.SLA, GETDATE())) as DateSLA,
                            CAST(vp.ProblemIDV1 AS VARCHAR(50)) + '#' + CAST(vp.ProblemIDV2 AS VARCHAR(50))  +  '#' + CAST(vp.ProblemIDV3 AS VARCHAR(50))  AS ProblemALLID
                            FROM [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Details] pdd
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Master] pdm ON pdd.ProblemDepartID = pdm.ProblemDepartID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].dbo.Problem_Depart_LineToken lt ON pdd.DepartID = lt.DepartID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].dbo.Problem_Master pm ON pdm.ProblemID = pm.ProblemID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].dbo.v_AllProblem vp ON pdm.ProblemID = vp.ProblemIDV3
                            WHERE pdd.ProblemDepartID IN (SELECT [ProblemDepartID]
                            FROM [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Master] WHERE ProblemID = @ProblemID)";
                            DataTable dt_depart = new DataTable();
                            dt_depart = connectdb.ExecuteDataTable(DbConnectionString, sql_Depart, new {
                                ProblemID = ProblemID
                            });

                            string sql_Problem_Respon_Master = @"
                                INSERT INTO [TSR_ONLINE_MARKETING].[dbo].Problem_Respon_Master (
                                    InformID,
                                    ProblemID,
                                    DepartID,
                                    WorkCode,
                                    date_create,
                                    date_modify,
                                    user_code,
                                    ipaddress,
                                    computername,
                                    ResponStatus
                                ) VALUES (
                                    @InformID,
                                    @ProblemID,
                                    @DepartID,
                                    @WorkCode,
                                    GETDATE(),
                                    GETDATE(),
                                    @user_code,
                                    @ipaddress,
                                    @computername,
                                    1
                                )
                            ";

                            foreach (DataRow dtRow in dt_depart.Rows)
                            {
                                string DepartID = dtRow["DepartID"].ToString();
                                conn.Execute(sql_Problem_Respon_Master, new
                                {
                                    InformID = InformID,
                                    ProblemID = ProblemID,
                                    DepartID = DepartID,
                                    WorkCode = WorkCode,
                                    user_code = UserCode,
                                    ipaddress = request.IpAddress,
                                    computername = request.DeviceName,
                                }, tran);
                            }


                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            // roll the transaction back
                            tran.Rollback();
                            return Ok(new
                            {
                                STATUS = 500,
                                MESSAGE = ex.Message.ToString(),
                            });
                        }
                    }
                }

                return Ok(new
                {
                    status = "SUCCESS",
                    message = "แจ้งปัญหาสำเร็จ",
                    InformID = InformID,
                });

            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    status = "FAILED",
                    message = ex.Message.ToString(),
                    InformID = "",
                });
            }
        }


    }


    public class RequestTicket
    {
        public string Contno { get; set; }
        public string ProductType { get; set; }
        public string ProblemDetails { get; set; }
        public string Tel { get; set; }
        public string DeviceName { get; set; }
        public string IpAddress { get; set; }
        public List<ObjectImage> Images { get; set; }
    }

    public class ObjectImage {
        public string data { get; set; }
        public string url { get; set; }
    }

  
}