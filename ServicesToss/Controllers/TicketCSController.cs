using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Configuration;
using System.Web.Http;
using Newtonsoft.Json;
using ServicesToss.Models;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Web.Script.Serialization;

namespace ServicesToss.Controllers
{
    public class TicketCSController : ApiController
    {

        [HttpGet, Route("TicketCS/OpenDocToIMindGET")]
        public IHttpActionResult TestGet()
        {
            return Ok(new
            {
                Status = 200,
                message = "Error",
                data = "TEST"
            });
        }

        /// API ปิดใบงาน Imind
        [HttpPost, Route("TicketCS/CloseDocFromIMind")]
        public IHttpActionResult CloseProblem(ObjectCloseProblem req)
        {
            string sqlLogQR = @"INSERT INTO [TSR_ONLINE_MARKETING].[dbo].[ServiceToss_Log] (
                [ProjectName]
                ,[ControllerName]
                ,[FunctionName]
                ,[JsonRequestData]
                ,[JsonResponsData]
                ,[CreateDate]
                ,[ModifyDate]
            ) VALUES (
                @ProjectName
                ,@ControllerName
                ,@FunctionName
                ,@JsonRequestData
                ,@JsonResponsData
                ,GETDATE()
                ,GETDATE()
            )";

            if (req == null) {
                return Ok(new
                {
                    STATUS = 500,
                    MESSAGE = "your parameter fomat is wrong!",
                });
            }

            if (req.Note == null)
            {
                return Ok(new
                {
                    STATUS = 500,
                    MESSAGE = "your parameter Note is wrong!",
                });
            }

            ConnectDB connLog = new ConnectDB();
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-UAT"].ConnectionString;
            try
            {
                DataTable dt = new DataTable();
                ConnectDB connectdb = new ConnectDB();
                DataTable dtDepart = new DataTable();
                string depertIDCS = "S1101";

                string sqlWhere = "";
                if (req.ReferenceId.IndexOf('-') != -1)
                { //// มี -
                    sqlWhere = @" InformID + '-' + CAST(ProblemID as VARCHAR(10))  ";
                }
                else { //// ไม่มี - 
                    sqlWhere = @" InformID + CAST(ProblemID as VARCHAR(10)) ";
                }

                string sqlDepartID = @"SELECT CASE WHEN COUNT(Items) > 0 THEN 'S1101' ELSE 'S1203' END as DepartID
                FROM TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details 
                WHERE " + sqlWhere + @" = @InformID AND DepartID = @DepartID ";

                dtDepart = connectdb.ExecuteDataTable(DbConnectionString, sqlDepartID, new
                {
                    InformID = req.ReferenceId
                    ,DepartID = depertIDCS
                });
                depertIDCS = dtDepart.Rows[0]["DepartID"].ToString();

                //// get items from Problem_Respon_Details
                string sqlGetItems = @"SELECT InformID,ProblemID,COUNT(Items) + 1 as Items FROM TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details 
                WHERE " + sqlWhere + @" = @InformID AND DepartID = @DepartID
                GROUP BY InformID,ProblemID ";
                dt = connectdb.ExecuteDataTable(DbConnectionString, sqlGetItems, new
                {
                    InformID = req.ReferenceId
                    ,DepartID = depertIDCS
                });
                if (dt.Rows.Count == 0) {
                    connLog.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "Ticket"
                        ,ControllerName = "TicketCS"
                        ,FunctionName = "CloseDocFromIMind",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                        ,JsonResponsData = new JavaScriptSerializer().Serialize(new {
                            STATUS = 500,
                            TYPE = "ReferenceId not found!",
                            MESSAGE = "ReferenceId not found!",
                        })
                    });
                    return Ok(new
                    {
                        STATUS = 500,
                        MESSAGE = "ReferenceId not found!",
                    });
                }

                int items = Int32.Parse(dt.Rows[0]["Items"].ToString());
                string InformID = dt.Rows[0]["InformID"].ToString();
                string ProblemID = dt.Rows[0]["ProblemID"].ToString();

                using (var conn = new SqlConnection(DbConnectionString))
                {
                    conn.Open();
                    // create the transaction
                    // You could use `var` instead of `SqlTransaction`
                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                        try
                        {
                            if (req.Note.Trim() != "ยกเลิก") { ////ถ้าปิดงาน
                                string sql = @" INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details
                                (
                                    InformID,
                                    ProblemID,
                                    DepartID,
                                    Items,
                                    ResponTypeID,
                                    EmpID,
                                    ResponDateTime,
                                    ResponNote,
                                    date_create,
                                    date_modify,
                                    user_code,
                                    ipaddress,
                                    computername,
                                    NoteData,
                                    ResponStatus) 
                                VALUES (
                                    @InformID,
                                    @ProblemID,
                                    @DepartID,
                                    @Items,
                                    '05',
                                    @EmpID,
                                    @ResponDateTime,
                                    @ResponNote,
                                    GETDATE(),
                                    GETDATE(),
                                    'IMind',
                                    'IMindSerive',
                                    'IMindSerive',
                                    null,
                                    1
                                    )
                                ";
                                conn.Execute(sql, new
                                {
                                    InformID = InformID
                                    ,
                                    ProblemID = ProblemID
                                    ,
                                    DepartID = depertIDCS
                                    ,
                                    Items = items
                                    ,
                                    EmpID = req.EmpID
                                    ,
                                    ResponDateTime = req.DateTime
                                    ,
                                    ResponNote = req.Note
                                }, tran);


                                foreach (string url in req.Images)
                                {
                                    string sqlImage = @"INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details_Images (
                                        InformID,
                                        ProblemID,
                                        DepartID,
                                        Items,
                                        ImageItem,
                                        ImageUrl,
                                        ImageName,
                                        ImageSize,
                                        ImageType
                                    )VALUES(
                                        @InformID,
                                        @ProblemID,
                                        @DepartID,
                                        @Items,
                                        (
                                            SELECT COUNT(ImageItem)+1 FROM TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details_Images
                                            WHERE InformID = @InformID AND 
                                            ProblemID = @ProblemID AND
                                            DepartID = @DepartID AND 
                                            Items = @Items
                                        ),
                                        @ImageUrl,
                                        NULL,
                                        NULL,
                                        NULL
                                    )
                                    ";
                                    conn.Execute(sqlImage, new
                                    {
                                        InformID = InformID
                                        ,
                                        ProblemID = Int32.Parse(ProblemID)
                                        ,
                                        DepartID = depertIDCS
                                        ,
                                        Items = items
                                        ,
                                        ImageUrl = url
                                    }, tran);
                                }

                                string sqlUpdateRespon = @"UPDATE TSR_ONLINE_MARKETING.dbo.Problem_Respon_Master 
                                SET 
                                    WorkCode = '10',
                                    CloseEmpID = @CloseEmpID,
                                    CloseDateTime = @CloseDateTime,
                                    CloseNote = @CloseNote
                                WHERE InformID = @InformID AND ProblemID = @ProblemID AND DepartID = @DepartID
                            ";
                                conn.Execute(sqlUpdateRespon, new
                                {
                                    CloseEmpID = req.EmpID
                                    ,
                                    CloseDateTime = req.DateTime
                                    ,
                                    CloseNote = req.Note
                                    ,
                                    InformID = InformID
                                    ,
                                    ProblemID = Int32.Parse(ProblemID)
                                    ,
                                    DepartID = depertIDCS
                                }, tran);

                                //string sqlUpdateMaster = @"UPDATE dbo.Problem_Inform_Master 
                                //    SET 
                                //        WorkCode = '10',
                                //        CloseEmpID = @CloseEmpID,
                                //        CloseDateTime = @CloseDateTime,
                                //        CloseNote = @CloseNote
                                //    WHERE InformID = @InformID
                                //";
                                //conn.Execute(sqlUpdateMaster, new
                                //{
                                //    CloseEmpID = req.EmpID
                                //    ,CloseDateTime = req.DateTime
                                //    ,CloseNote = req.Note
                                //    ,InformID = req.ReferenceId
                                //}, tran);

                                tran.Commit();

                                var output = conn.Query("TSR_ONLINE_MARKETING.dbo.sp_ticket_CheckCloseProblem",
                                new
                                {
                                    v_InformID = InformID
                                }, commandType: CommandType.StoredProcedure);
                            }

                            if (req.Note.Trim() == "ยกเลิก") { //// ถ้ายกเลิก
                                string sql = @" INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details
                                (
                                    InformID,
                                    ProblemID,
                                    DepartID,
                                    Items,
                                    ResponTypeID,
                                    EmpID,
                                    ResponDateTime,
                                    ResponNote,
                                    date_create,
                                    date_modify,
                                    user_code,
                                    ipaddress,
                                    computername,
                                    NoteData,
                                    ResponStatus) 
                                VALUES (
                                    @InformID,
                                    @ProblemID,
                                    @DepartID,
                                    @Items,
                                    '99',
                                    @EmpID,
                                    @ResponDateTime,
                                    @ResponNote,
                                    GETDATE(),
                                    GETDATE(),
                                    'IMind',
                                    'IMindSerive',
                                    'IMindSerive',
                                    null,
                                    1
                                    )
                                ";
                                conn.Execute(sql, new
                                {
                                    InformID = InformID
                                    ,
                                    ProblemID = ProblemID
                                    ,
                                    DepartID = depertIDCS
                                    ,
                                    Items = items
                                    ,
                                    EmpID = req.EmpID
                                    ,
                                    ResponDateTime = req.DateTime
                                    ,
                                    ResponNote = req.Note
                                }, tran);


                                foreach (string url in req.Images)
                                {
                                    string sqlImage = @"INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details_Images (
                                        InformID,
                                        ProblemID,
                                        DepartID,
                                        Items,
                                        ImageItem,
                                        ImageUrl,
                                        ImageName,
                                        ImageSize,
                                        ImageType
                                    )VALUES(
                                        @InformID,
                                        @ProblemID,
                                        @DepartID,
                                        @Items,
                                        (
                                            SELECT COUNT(ImageItem)+1 FROM TSR_ONLINE_MARKETING.dbo.Problem_Respon_Details_Images
                                            WHERE InformID = @InformID AND 
                                            ProblemID = @ProblemID AND
                                            DepartID = @DepartID AND 
                                            Items = @Items
                                        ),
                                        @ImageUrl,
                                        NULL,
                                        NULL,
                                        NULL
                                    )
                                    ";
                                    conn.Execute(sqlImage, new
                                    {
                                        InformID = InformID
                                        ,
                                        ProblemID = Int32.Parse(ProblemID)
                                        ,
                                        DepartID = depertIDCS
                                        ,
                                        Items = items
                                        ,
                                        ImageUrl = url
                                    }, tran);
                                }

                                string sqlUpdateRespon = @"UPDATE TSR_ONLINE_MARKETING.dbo.Problem_Respon_Master 
                                SET 
                                    WorkCode = '90',
                                    CancelEmpID = @CloseEmpID,
                                    CancelDateTime = @CloseDateTime,
                                    CancelNote = @CloseNote
                                WHERE InformID = @InformID AND ProblemID = @ProblemID AND DepartID = @DepartID
                            ";
                                conn.Execute(sqlUpdateRespon, new
                                {
                                    CloseEmpID = req.EmpID
                                    ,
                                    CloseDateTime = req.DateTime
                                    ,
                                    CloseNote = req.Note
                                    ,
                                    InformID = InformID
                                    ,
                                    ProblemID = Int32.Parse(ProblemID)
                                    ,
                                    DepartID = depertIDCS
                                }, tran);

                                tran.Commit();

                                var output = conn.Query("TSR_ONLINE_MARKETING.dbo.sp_ticket_CheckCancelProblem",
                                  new
                                  {
                                      v_InformID = InformID
                                  }, commandType: CommandType.StoredProcedure);
                            }
                        }
                        catch (Exception ex)
                        {
                            // roll the transaction back
                            tran.Rollback();
                            //// insert log
                            connLog.InsertData(DbConnectionString, sqlLogQR, new
                            {
                                ProjectName = "Ticket"
                                ,ControllerName = "TicketCS"
                                ,FunctionName = "CloseDocFromIMind",
                                JsonRequestData = new JavaScriptSerializer().Serialize(req)
                                ,JsonResponsData = new JavaScriptSerializer().Serialize(new {
                                    STATUS = 500,
                                    TYPE = "transaction Rollback",
                                    MESSAGE = ex.Message.ToString(),
                                })
                            });
                            return Ok(new
                            {
                                STATUS = 500,
                                MESSAGE = ex.Message.ToString(),
                            });
                        }
                    }
                }
                //// insert log
                connLog.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "Ticket"
                    ,ControllerName = "TicketCS"
                    ,FunctionName = "CloseDocFromIMind",
                    JsonRequestData = new JavaScriptSerializer().Serialize(req)
                    ,JsonResponsData = new JavaScriptSerializer().Serialize(new {
                        STATUS = 200,
                        MESSAGE = "Successful",
                    })
                });
                return Ok(new
                {
                    STATUS = 200,
                    MESSAGE = "Successful",
                });
            }
            catch (Exception ex)
            {
                //// insert log
                connLog.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "Ticket"
                    ,ControllerName = "TicketCS"
                    ,FunctionName = "CloseDocFromIMind",
                    JsonRequestData = new JavaScriptSerializer().Serialize(req)
                    ,JsonResponsData = new JavaScriptSerializer().Serialize(new {
                        STATUS = 500,
                        MESSAGE = ex.Message.ToString(),
                    })
                });
                return Ok(new
                {
                    STATUS = 500,
                    MESSAGE = ex.Message.ToString(),
                });
            }

        }



        /// API เปิดใบงาน Imind
        [HttpPost, Route("TicketCS/OpenDocToIMindProduction")] 
        public IHttpActionResult OpenDocToIMindProduction(ObjRequest req)
        {

            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            string[] ProblemID = req.ProblemID.Split('#');
            //ReturnJSON result = new ReturnJSON();
            try
            {
                
                iMindForTOSSProduction.TicketServiceSoapClient tosWS = new iMindForTOSSProduction.TicketServiceSoapClient();
                iMindForTOSSProduction.TosTicket tosValue = new iMindForTOSSProduction.TosTicket();
                iMindForTOSSProduction.ServiceResponseOfTosTicketResult tosResult = new iMindForTOSSProduction.ServiceResponseOfTosTicketResult();

                tosValue.ReferenceId = string.IsNullOrEmpty(req.InformID) ? "" : req.InformID.Trim();
                tosValue.InformDateTime = string.IsNullOrEmpty(req.InformDateTime) ? "" : req.InformDateTime.Trim();
                tosValue.ProblemId = string.IsNullOrEmpty(req.ProblemID) ? "" : req.ProblemID.Trim();
                tosValue.ProblemName = string.IsNullOrEmpty(req.ProblemName) ? "" : req.ProblemName.Trim(); //// 
                tosValue.Contno = string.IsNullOrEmpty(req.Contno) ? "" : req.Contno.Trim(); ////
                tosValue.CustomerName = string.IsNullOrEmpty(req.CustomerName) ? "" : req.CustomerName.Trim(); ////
                tosValue.Tel = string.IsNullOrEmpty(req.Tel) ? "" : req.Tel.Trim();
                tosValue.AddressDetail = string.IsNullOrEmpty(req.AddressDetail) ? "" : req.AddressDetail;
                tosValue.District = string.IsNullOrEmpty(req.District) ? "" :  req.District;//string.IsNullOrEmpty(req.District) ? "" : dt.Rows[0]["DISTRICT_NAME"].ToString();
                tosValue.Amphur = string.IsNullOrEmpty(req.Amphur) ? "" :  req.Amphur;// string.IsNullOrEmpty(req.Amphur) ? "" : dt.Rows[0]["AMPHUR_NAME"].ToString();
                tosValue.Province = string.IsNullOrEmpty(req.Province) ? "" :  req.Province;// string.IsNullOrEmpty(req.Province) ? "" : dt.Rows[0]["PROVINCE_NAME"].ToString();
                tosValue.Zipcode = string.IsNullOrEmpty(req.Zipcode) ? "" : req.Zipcode;

                tosResult = tosWS.SaveTicketTOS(tosValue);

                string sql = @"UPDATE [TSR_ONLINE_MARKETING].[dbo].[Problem_Respon_Master] 
                    SET 
                        IMind_StatusCode = @IMind_StatusCode,
                        IMind_Message = @IMind_Message,
                        IMind_TicketNumber = @IMind_TicketNumber
                    WHERE InformID + '-' + CAST(ProblemID as VARCHAR(10)) = @InformID /*AND ProblemID = @ProblemID*/ AND DepartID = @DepartID
                    ";
                var res = conn.InsertData(DbConnectionString, sql, new
                {
                    IMind_StatusCode = tosResult.Data != null ? "200"/*tosResult.StatusCode.ToString() */: "404",
                    IMind_Message = tosResult.Message.Replace("'", "''").ToString(),
                    IMind_TicketNumber = tosResult.Data != null ? tosResult.Data.TicketNumber.ToString() : "",
                    InformID = req.InformID,
                    //ProblemID = ProblemID[2],
                    DepartID = req.DepartID
                });

                return Ok(new
                {
                    Status = tosResult.Data != null ? 200 : 400,
                    message = tosResult.Data != null ? "Success" : tosResult.Message.Replace("'", "''").ToString(),
                    data = tosResult
                });
            }
            catch (Exception ex)
            {
                string sql = @"UPDATE [TSR_ONLINE_MARKETING].[dbo].[Problem_Respon_Master] 
                SET 
                    IMind_StatusCode = '404',
                    IMind_Message = @IMind_Message
                WHERE InformID + '-' + CAST(ProblemID as VARCHAR(10)) = @InformID /*AND ProblemID = @ProblemID*/ AND DepartID = @DepartID
                ";
                var res = conn.InsertData(DbConnectionString, sql, new
                {
                    IMind_Message = ex.Message.ToString(),
                    InformID = req.InformID,
                    //ProblemID = ProblemID[2],
                    DepartID = req.DepartID
                });
                return Ok(new
                {
                    Status = 500,
                    message = "Error",
                    data = ex.Message.ToString()
                });
            }

        }



        [HttpPost, Route("TicketCS/OpenDocToIMindUAT")]
        public IHttpActionResult OpenDocToIMindUAT(ObjRequest req)
        {
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-UAT"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            string[] ProblemID = req.ProblemID.Split('#');
            //ReturnJSON result = new ReturnJSON();
            try
            {
                //string sqlAddr = @"select d.DISTRICT_NAME,a.AMPHUR_NAME,p.PROVINCE_NAME
                //from TSR_Application.dbo.DebtorAnalyze_District d
                //INNER JOIN TSR_Application.dbo.DebtorAnalyze_Amphur a on d.AMPHUR_ID = a.AMPHUR_ID
                //INNER JOIN TSR_Application.dbo.DebtorAnalyze_Province p on d.PROVINCE_ID = p.PROVINCE_ID
                //WHERE d.DISTRICT_ID = @DISTRICT_ID
                //";
                //DataTable dt = new DataTable();
                //dt = conn.ExecuteDataTable(DbConnectionString, sqlAddr, new
                //{
                //    DISTRICT_ID = req.District
                //});

                iMindForTossUAT.TicketServiceSoapClient tosWS = new iMindForTossUAT.TicketServiceSoapClient();
                iMindForTossUAT.TosTicket tosValue = new iMindForTossUAT.TosTicket();
                iMindForTossUAT.ServiceResponseOfTosTicketResult tosResult = new iMindForTossUAT.ServiceResponseOfTosTicketResult();


                tosValue.ReferenceId = string.IsNullOrEmpty(req.InformID) ? "" : req.InformID;
                tosValue.InformDateTime = string.IsNullOrEmpty(req.InformDateTime) ? "" : req.InformDateTime;
                tosValue.ProblemId = string.IsNullOrEmpty(req.ProblemID) ? "" : req.ProblemID;
                tosValue.ProblemName = string.IsNullOrEmpty(req.ProblemName) ? "" : req.ProblemName; //// 
                tosValue.Contno = string.IsNullOrEmpty(req.Contno) ? "" : req.Contno; ////
                tosValue.CustomerName = string.IsNullOrEmpty(req.CustomerName) ? "" : req.CustomerName; ////
                tosValue.Tel = string.IsNullOrEmpty(req.Tel) ? "" : req.Tel;
                tosValue.AddressDetail = string.IsNullOrEmpty(req.AddressDetail) ? "" : req.AddressDetail;
                tosValue.District = req.District;//string.IsNullOrEmpty(req.District) ? "" : dt.Rows[0]["DISTRICT_NAME"].ToString();
                tosValue.Amphur = req.Amphur;// string.IsNullOrEmpty(req.Amphur) ? "" : dt.Rows[0]["AMPHUR_NAME"].ToString();
                tosValue.Province = req.Province;// string.IsNullOrEmpty(req.Province) ? "" : dt.Rows[0]["PROVINCE_NAME"].ToString();
                tosValue.Zipcode = string.IsNullOrEmpty(req.Zipcode) ? "" : req.Zipcode;

                tosResult = tosWS.SaveTicketTOS(tosValue);

                string sql = @"UPDATE [TSR_ONLINE_MARKETING].[dbo].[Problem_Respon_Master] 
                SET 
                    IMind_StatusCode = @IMind_StatusCode,
                    IMind_Message = @IMind_Message,
                    IMind_TicketNumber = @IMind_TicketNumber
                WHERE InformID + '-' + CAST(ProblemID as VARCHAR(10)) = @InformID /*AND ProblemID = @ProblemID*/ AND DepartID = @DepartID
                ";
                var res = conn.InsertData(DbConnectionString, sql, new
                {
                    IMind_StatusCode = tosResult.StatusCode.ToString(),
                    IMind_Message = tosResult.Message.Replace("'", "''").ToString(),
                    IMind_TicketNumber = tosResult.Data != null ? tosResult.Data.TicketNumber.ToString() : "",
                    InformID = req.InformID,
                    //ProblemID = ProblemID[2],
                    DepartID = req.DepartID
                });

                return Ok(new
                {
                    Status = 200,
                    message = "Success",
                    data = tosResult
                });
            }
            catch (Exception ex)
            {
                string sql = @"UPDATE [TSR_ONLINE_MARKETING].[dbo].[Problem_Respon_Master] 
                SET 
                    IMind_StatusCode = '500',
                    IMind_Message = @IMind_Message,
                WHERE InformID + '-' + CAST(ProblemID as VARCHAR(10)) = @InformID /*AND ProblemID = @ProblemID */ AND DepartID = @DepartID
                ";
                var res = conn.InsertData(DbConnectionString, sql, new
                {
                    IMind_Message = ex.Message.ToString(),
                    InformID = req.InformID,
                    //ProblemID = ProblemID[2],
                    DepartID = req.DepartID
                });
                return Ok(new
                {
                    Status = 200,
                    message = "Error",
                    data = ex.Message.ToString()
                });
            }

        }

    }


    public class ObjRequest {
        public string InformID { get; set; }
        public string InformDateTime { get; set; }
        public string ProblemID { get; set; }
        public string DepartID { get; set; }
        public string ProblemName { get; set; }
        public string Contno { get; set; }
        public string CustomerName { get; set; }
        public string Tel { get; set; }
        public string AddressDetail { get; set; }
        public string District { get; set; }
        public string Amphur { get; set; }
        public string Province { get; set; }
        public string Zipcode { get; set; }
    }

    public class ObjectCloseProblem {
        public string ReferenceId { get; set; }
       // public string ActionType { get; set; }
        //public string ProblemId { get; set; }
        public DateTime DateTime { get; set; }
        public string Note { get; set; }
        public string EmpID { get; set; }
        public List<string> Images { get; set; }
    }



    
}


