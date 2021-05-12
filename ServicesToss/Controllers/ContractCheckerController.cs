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
    public class ContractCheckerController : ApiController
    {
        // GET: ContractChecker

        // GET: SmartCare
        [HttpGet, Route("api/ContractChecker/Example")]
        public IHttpActionResult Index()
        {
            return Ok(new
            {
                status = "200",
                message = "ContractChecker hello world",
            });
        }


        // POST: OpenTicket
        [HttpPost, Route("api/ContractChecker/OpenTicket")]
        public IHttpActionResult OpenTicketFromSmartCare(RequestContractChecker request)
        {
        
            //string DbConnectionString = WebConfigurationManager.ConnectionStrings[Properties.Settings.Default.ConnectionType == "UAT" ? "TSR-SQL-UAT" : "TSR-SQL-02"].ConnectionString;
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            try
            {
                /* Create Object Instance */
                ConnectDB connectdb = new ConnectDB();
                string sqlInformID = @"SELECT [TSR_ONLINE_MARKETING].[dbo].[GenProblemID]() AS InformID";
                DataTable dt = new DataTable();
                dt = connectdb.ExecuteDataTable(DbConnectionString, sqlInformID, null);
                if (dt.Rows.Count == 0)
                {
                    return Ok(new
                    {
                        status = "SUCCESS",
                        message = "Can't create Inform Number!",
                        InformID = "",
                    });
                }
                string InformID = dt.Rows[0]["InformID"].ToString();
                string WorkCode = "22";
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
                                    GUID,
                                    Contno,
                                    InformEmpID,
                                    InformDepartID,
                                    WorkCode,
                                    date_create,
                                    date_modify,
                                    user_code,
                                    ipaddress,
                                    computername,
                                    InformStatus,
                                    DataChannel,
                                    SaleCode,
                                    SaleEmpID
                                ) SELECT 
                                    @InformID,
                                    GETDATE(),
                                    @GUID,
                                    @Contno,
                                    @InformEmpID,
                                    @InformDepartID,
                                    @WorkCode,
                                    GETDATE(),
                                    GETDATE(),
                                    @user_code,
                                    @ipaddress,
                                    @computername,
                                    1,
                                    '04',
                                    ContractSale,
                                    ContractSaleEmp
                                FROM TSR_DB1.dbo.CONTRACT_INFO where Refno = @Refno AND IsActive = 1
                            ";
                            conn.Execute(sql_Insert_Inform_Master, new
                            {
                                InformID = InformID,
                                Contno = request.Contno,
                                GUID = request.Contno,
                                InformEmpID = request.InformEmpID,
                                InformDepartID = request.InformDepartID,
                                WorkCode = WorkCode,
                                user_code = request.InformEmpID,
                                ipaddress = request.IpAddress,
                                computername = request.DeviceName,
                                Refno = request.Contno
                            }, tran);

                            string sql_Insert_Inform_Detail = @"
                                INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Inform_Details (
                                    InformID,
                                    ProblemID,
                                    ProblemDetail,
                                    ProblemStatus 
                                ) VALUES (
                                    @InformID,
                                    @ProblemID,
                                    @ProblemDetail,
                                    1
                                )
                            ";
                            conn.Execute(sql_Insert_Inform_Detail, new
                            {
                                InformID = InformID,
                                ProblemID = request.ProblemID,
                                ProblemDetail = request.ProblemDetails,
                            }, tran);


                            string sql_Depart = @"SELECT pdd.[DepartID],
                            (SELECT TOP 1 ContractSaleEmp FROM TSR_DB1.dbo.Contract_INFO where Refno = @Refno AND isActive = 1) as SaleEmployeeCode,
                            [TSR_ONLINE_MARKETING].[dbo].ConvertProblemIDToProblemFullName(pdm.ProblemID) AS ProblemName,
                            ISNULL(lt.LineToken,'') AS LineToken,
                            pm.SLA,
                            [TSR_ONLINE_MARKETING].[dbo].fn_ConvertDateSqlToDateThai(DATEADD(HOUR, pm.SLA, GETDATE())) as DateSLA,
                            CAST(vp.ProblemIDV1 AS VARCHAR(50)) + '#' + CAST(vp.ProblemIDV2 AS VARCHAR(50))  +  '#' + CAST(vp.ProblemIDV3 AS VARCHAR(50))  AS ProblemALLID
                            FROM [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Details] pdd
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Master] pdm ON pdd.ProblemDepartID = pdm.ProblemDepartID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].Problem_Depart_LineToken lt ON pdd.DepartID = lt.DepartID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].Problem_Master pm ON pdm.ProblemID = pm.ProblemID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].v_AllProblem vp ON pdm.ProblemID = vp.ProblemIDV3
                            WHERE pdd.ProblemDepartID IN (SELECT [ProblemDepartID]
                            FROM [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Master] WHERE ProblemID = @ProblemID)";
                            DataTable dt_depart = new DataTable();
                            dt_depart = connectdb.ExecuteDataTable(DbConnectionString, sql_Depart, new
                            {
                                ProblemID = request.ProblemID,
                                Refno = request.Contno
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


                            string sqlResponDetail = @"Insert INTO [TSR_ONLINE_MARKETING].[dbo].Problem_Respon_Details (
                                [InformID]
                                ,[ProblemID]
                                ,[DepartID]
                                ,[Items]
                                ,[ResponTypeID]
                                ,[EmpID]
                                ,[ResponDateTime]
                                ,[date_create]
                                ,[user_code]
                                ,[ipaddress]
                                ,[computername]
                                ,[ResponStatus]) 
                                SELECT 
                                    @InformID,
                                    @ProblemID,
                                    @DepartID,
                                    1,
                                    '01',
                                    ContractSaleEmp,
                                    GETDATE(),
                                    GETDATE(),
                                    @user_code,
                                    @ipaddress,
                                    @computername,
                                    1
                                FROM TSR_DB1.dbo.Contract_INFO where Refno = @Refno AND isActive = 1

                            ";

                            foreach (DataRow dtRow in dt_depart.Rows)
                            {
                                string DepartID = dtRow["DepartID"].ToString();
                                conn.Execute(sql_Problem_Respon_Master, new
                                {
                                    InformID = InformID,
                                    ProblemID = request.ProblemID,
                                    DepartID = DepartID,
                                    WorkCode = WorkCode,
                                    user_code = request.InformEmpID,
                                    ipaddress = request.IpAddress,
                                    computername = request.DeviceName,
                                }, tran);


                                conn.Execute(sqlResponDetail, new
                                {
                                    InformID = InformID,
                                    ProblemID = request.ProblemID,
                                    DepartID = DepartID,
                                    user_code = request.InformEmpID,
                                    ipaddress = request.IpAddress,
                                    computername = request.DeviceName,
                                    Refno = request.Contno
                                }, tran);

                                ///// SEND Notification TO mobile toss checker
                                string ApiNotificationMobile = @"http://app.thiensurat.co.th/assanee/api_sale_all_problem_from_cedit_by_db_kiw/firebase_nontification_sale_from_web_to_sale_all2/index.php?sale_empid=" + dtRow["SaleEmployeeCode"].ToString() + "&contno=" + request.Contno+ "&problem=" + dtRow["ProblemName"].ToString();
                                HttpWebRequest CallNotification = (HttpWebRequest)WebRequest.Create(ApiNotificationMobile);
                                CallNotification.Method = "GET";
                                var response = (HttpWebResponse)CallNotification.GetResponse();
                                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
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




        // POST: OpenTicket
        [HttpPost, Route("api/ContractChecker/OpenTicketUAT")]
        public IHttpActionResult OpenTicketUAT(RequestContractChecker request)
        {
            //string DbConnectionString = WebConfigurationManager.ConnectionStrings[Properties.Settings.Default.ConnectionType == "UAT" ? "TSR-SQL-UAT" : "TSR-SQL-02"].ConnectionString;
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-UAT"].ConnectionString;
            try
            {
                /* Create Object Instance */
                ConnectDB connectdb = new ConnectDB();
                string sqlInformID = @"SELECT [TSR_ONLINE_MARKETING].[dbo].[GenProblemID]() AS InformID";
                DataTable dt = new DataTable();
                dt = connectdb.ExecuteDataTable(DbConnectionString, sqlInformID, null);
                if (dt.Rows.Count == 0)
                {
                    return Ok(new
                    {
                        status = "SUCCESS",
                        message = "Can't create Inform Number!",
                        InformID = "",
                    });
                }
                string InformID = dt.Rows[0]["InformID"].ToString();
                string WorkCode = "22";
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
                                    GUID,
                                    Contno,
                                    InformEmpID,
                                    InformDepartID,
                                    WorkCode,
                                    date_create,
                                    date_modify,
                                    user_code,
                                    ipaddress,
                                    computername,
                                    InformStatus,
                                    DataChannel,
                                    SaleCode,
                                    SaleEmpID
                                ) SELECT top 1
                                    @InformID,
                                    GETDATE(),
                                    @GUID,
                                    @Contno,
                                    @InformEmpID,
                                    @InformDepartID,
                                    @WorkCode,
                                    GETDATE(),
                                    GETDATE(),
                                    @user_code,
                                    @ipaddress,
                                    @computername,
                                    1,
                                    '04',
                                    ContractSale,
                                    ContractSaleEmp
                                FROM TSR_DB1.dbo.CONTRACT_INFO where Refno = @Refno AND IsActive = 1
                            ";
                            conn.Execute(sql_Insert_Inform_Master, new
                            {
                                InformID = InformID,
                                Contno = request.Contno,
                                GUID = request.Contno,
                                InformEmpID = request.InformEmpID,
                                InformDepartID = request.InformDepartID,
                                WorkCode = WorkCode,
                                user_code = request.InformEmpID,
                                ipaddress = request.IpAddress,
                                computername = request.DeviceName,
                                Refno = request.Contno
                            }, tran);

                            string sql_Insert_Inform_Detail = @"
                                INSERT INTO TSR_ONLINE_MARKETING.dbo.Problem_Inform_Details (
                                    InformID,
                                    ProblemID,
                                    ProblemDetail,
                                    ProblemStatus 
                                ) VALUES (
                                    @InformID,
                                    @ProblemID,
                                    @ProblemDetail,
                                    1
                                )
                            ";
                            conn.Execute(sql_Insert_Inform_Detail, new
                            {
                                InformID = InformID,
                                ProblemID = request.ProblemID,
                                ProblemDetail = request.ProblemDetails,
                            }, tran);


                            string sql_Depart = @"SELECT pdd.[DepartID],
                            (SELECT TOP 1 ContractSaleEmp FROM TSR_DB1.dbo.Contract_INFO where Refno = @Refno AND isActive = 1) as SaleEmployeeCode,
                            [TSR_ONLINE_MARKETING].[dbo].ConvertProblemIDToProblemFullName(pdm.ProblemID) AS ProblemName,
                            ISNULL(lt.LineToken,'') AS LineToken,
                            pm.SLA,
                            [TSR_ONLINE_MARKETING].[dbo].fn_ConvertDateSqlToDateThai(DATEADD(HOUR, pm.SLA, GETDATE())) as DateSLA,
                            CAST(vp.ProblemIDV1 AS VARCHAR(50)) + '#' + CAST(vp.ProblemIDV2 AS VARCHAR(50))  +  '#' + CAST(vp.ProblemIDV3 AS VARCHAR(50))  AS ProblemALLID
                            FROM [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Details] pdd
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Master] pdm ON pdd.ProblemDepartID = pdm.ProblemDepartID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].Problem_Depart_LineToken lt ON pdd.DepartID = lt.DepartID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].Problem_Master pm ON pdm.ProblemID = pm.ProblemID
                            LEFT JOIN [TSR_ONLINE_MARKETING].[dbo].v_AllProblem vp ON pdm.ProblemID = vp.ProblemIDV3
                            WHERE pdd.ProblemDepartID IN (SELECT [ProblemDepartID]
                            FROM [TSR_ONLINE_MARKETING].[dbo].[Problem_Depart_Master] WHERE ProblemID = @ProblemID)";
                            DataTable dt_depart = new DataTable();
                            dt_depart = connectdb.ExecuteDataTable(DbConnectionString, sql_Depart, new
                            {
                                ProblemID = request.ProblemID,
                                Refno = request.Contno
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


                            string sqlResponDetail = @"Insert INTO [TSR_ONLINE_MARKETING].[dbo].Problem_Respon_Details (
                                [InformID]
                                ,[ProblemID]
                                ,[DepartID]
                                ,[Items]
                                ,[ResponTypeID]
                                ,[EmpID]
                                ,[ResponDateTime]
                                ,[date_create]
                                ,[user_code]
                                ,[ipaddress]
                                ,[computername]
                                ,[ResponStatus]) 
                                SELECT top 1
                                    @InformID,
                                    @ProblemID,
                                    @DepartID,
                                    1,
                                    '01',
                                    ContractSaleEmp,
                                    GETDATE(),
                                    GETDATE(),
                                    @user_code,
                                    @ipaddress,
                                    @computername,
                                    1
                                FROM TSR_DB1.dbo.Contract_INFO where Refno = @Refno AND isActive = 1

                            ";

                            foreach (DataRow dtRow in dt_depart.Rows)
                            {
                                string DepartID = dtRow["DepartID"].ToString();
                                conn.Execute(sql_Problem_Respon_Master, new
                                {
                                    InformID = InformID,
                                    ProblemID = request.ProblemID,
                                    DepartID = DepartID,
                                    WorkCode = WorkCode,
                                    user_code = request.InformEmpID,
                                    ipaddress = request.IpAddress,
                                    computername = request.DeviceName,
                                }, tran);


                                conn.Execute(sqlResponDetail, new
                                {
                                    InformID = InformID,
                                    ProblemID = request.ProblemID,
                                    DepartID = DepartID,
                                    user_code = request.InformEmpID,
                                    ipaddress = request.IpAddress,
                                    computername = request.DeviceName,
                                    Refno = request.Contno
                                }, tran);

                                ///// SEND Notification TO mobile toss checker
                                //string ApiNotificationMobile = @"http://app.thiensurat.co.th/assanee/api_sale_all_problem_from_cedit_by_db_kiw/firebase_nontification_sale_from_web_to_sale_all2/index.php?sale_empid=" + dtRow["SaleEmployeeCode"].ToString() + "&contno=" + request.Contno + "&problem=" + dtRow["ProblemName"].ToString();
                                //HttpWebRequest CallNotification = (HttpWebRequest)WebRequest.Create(ApiNotificationMobile);
                                //CallNotification.Method = "GET";
                                //var response = (HttpWebResponse)CallNotification.GetResponse();
                                //var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
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



    public class RequestContractChecker
    {
        public string Contno { get; set; }
        public int ProblemID { get; set; }
        public string ProblemDetails { get; set; }

        public string InformEmpID { get; set; }
        public string InformDepartID { get; set; }
        public string DeviceName { get; set; }
        public string IpAddress { get; set; }
    }

}