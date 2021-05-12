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
using WebSocketSharp;
using System.Data;
using System.Web.Script.Serialization;
using System.Data.SqlClient;
using Dapper;

namespace ServicesToss.Controllers
{
    public class CheckedController : ApiController
    {
        [HttpPost, Route("CheckerCard/ImportContno")]
        public IHttpActionResult TransferHomeRedToToss(RequestImportContno req) {
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
            ///TSR-SQL-02
            ////// TSR-SQL-UAT
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON returnFromCards = new ReturnJSON();
            ReturnJSON returnFromCardsLog = new ReturnJSON();
            try
            {
                if (string.IsNullOrEmpty(req.Contno) || string.IsNullOrEmpty(req.Refno)) { //// ถ้า parameter is null or empty
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker" ,
                        ControllerName = "CheckerCard",
                        FunctionName = "ImportContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                        ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 501,
                            TYPE = "ERROR!",
                            MESSAGE = "Parameter Contno,Refno is Null or Empty!",
                        })
                    });
                    return Ok(new
                    {
                        status = 501,
                        message = "ERROR",
                        description = "Parameter Contno,Refno is Null or Empty",
                    });
                }

                string sqlData = @"select c.*,
                (
                    select count(f.contno) 
                    from TSR_ONLINE_MARKETING.dbo.CheckerCard_FromCards f where c.Contno = f.Contno AND c.Refno = f.Refno 
                ) as CountCheck
                from tsrdata.dbo.checkercard c where c.contno = @Contno and c.refno = @Refno ";
                DataTable dtData= new DataTable();
                dtData = conn.ExecuteDataTable(DbConnectionString, sqlData, new
                {
                    Contno = req.Contno,
                    Refno = req.Refno
                });
                if (dtData.Rows.Count == 0) { //// ถ้าไม่มีข้อมูล
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "ImportContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                        ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 502,
                            TYPE = "ERROR!",
                            MESSAGE = "Contno,Refno not found!",
                        })
                    });
                    return Ok(new
                    {
                        status = 502,
                        message = "ERROR",
                        description = "Contno,Refno not found!",
                    });
                }
                if (int.Parse(dtData.Rows[0]["CountCheck"].ToString()) > 0) { ///// ถ้าข้อมูลเคยส่งมาแล้ว
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "ImportContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                        ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 503,
                            TYPE = "ERROR duplicate!",
                            MESSAGE = "Contno,Refno is duplicate!",
                        })
                    });
                    return Ok(new
                    {
                        status = 503,
                        message = "ERROR",
                        description = "Contno,Refno is duplicate!",
                    });
                }

                string sqlStatusEmp = @"
                 SELECT 
	                subquery.id,
	                subquery.contno,
	                subquery.refno,
	                subquery.docno,
	                subquery.docdate,
	                subquery.addr1,
	                subquery.addr2,
	                subquery.addr3,
	                subquery.addr4,
	                subquery.addrzip,
	                subquery.amonth,
	                subquery.ayear,
	                subquery.acode,
	                subquery.empid,
	                CASE   
		                WHEN subquery.StatusEmp = 0 THEN 'FALSE'
		                ELSE 'TRUE'
	                END   AS StatusEmp,
	                CASE   
		                WHEN subquery.StatusEmp = 0 THEN 'รหัส acode กับ รหัส empid ไม่ตรงกัน'
		                ELSE ''
	                END + ' ' +
	                CASE   
		                WHEN subquery.acode = '' THEN 'รหัส acode ไม่มีข้อมูล'
		                ELSE ''
	                END + ' ' +
	                CASE   
		                WHEN subquery.empid = '' THEN 'รหัส empid ไม่มีข้อมูล'
		                ELSE ''
	                END
	                AS StatusDescription
	                FROM (
	                SELECT c.id,
	                c.contno,
	                c.refno,
	                c.docno,
	                c.docdate,
	                c.addr1,
	                c.addr2,
	                c.addr3,
	                c.addr4,
	                c.addrzip,
	                c.amonth,
	                c.ayear,
	                ISNULL(c.acode,'') AS acode,
	                ISNULL(c.empid,'') AS empid,
	                (
		                SELECT COUNT(*) AS Num FROM TSRDATA.dbo.CAreaTripA t WHERE t.acode = c.acode AND t.empid = c.empid AND t.amonth = c.amonth 
		                AND t.ayear = c.ayear
	                ) AS StatusEmp
	                FROM TSRDATA.dbo.checkercard c
	                WHERE c.Contno = @Contno AND Refno = @Refno
                ) AS subquery 
                WHERE subquery.acode  = '' OR subquery.empid = '' OR subquery.StatusEmp = 0 ";
                DataTable dtStatusEmp = new DataTable();
                dtStatusEmp = conn.ExecuteDataTable(DbConnectionString,sqlStatusEmp,new {
                    Contno = req.Contno,
                    Refno = req.Refno
                });
                if (dtStatusEmp.Rows.Count > 0) {
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "ImportContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                       ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 504,
                            TYPE = "ERROR!",
                            MESSAGE = dtStatusEmp.Rows[0]["StatusDescription"].ToString(),
                        })
                    });
                    return Ok(new
                    {
                        status = 504,
                        message = "ERROR",
                        description = dtStatusEmp.Rows[0]["StatusDescription"].ToString(),
                    });
                }

                string sqlInsertFromcards = @"INSERT INTO [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_FromCards]
                    (
                        [id]
                        ,[docno]
                        ,[amonth]
                        ,[ayear]
                        ,[docdate]
                        ,[refno]
                        ,[contno]
                        ,[name]
                        ,[cashcode]
                        ,[acode]
                        ,[paydate]
                        ,[senddate]
                        ,[note]
                        ,[addr1]
                        ,[addr2]
                        ,[addr3]
                        ,[addr4]
                        ,[addrzip]
                        ,[tumbon]
                        ,[lock]
                        ,[provinceid]
                        ,[districtid]
                        ,[amphurid]
                        ,[zip]
                        ,[adate]
                        ,[meetdate]
                        ,[alock]
                        ,[empid]
                        ,[status]
                        ,[InformID]
                        ,[NoID]
                    )
                    SELECT 
                        id,
			            docno,
			            amonth,
			            ayear,
			            docdate,
			            refno,
			            contno,
			            name,
			            isnull(cashcode,ccode) as cashcode,
			            acode,
			            paydate,
			            senddate,
			            note,
			            addr1,
			            addr2,
			            addr3,
			            addr4,
			            addrzip,
			            tumbon,
			            lock,
			            provinceid,
			            districtid,
			            amphurid,
			            zip,
			            adate,
			            meetdate,
			            alock,
			            empid,
			            '0' AS status ,
			            NULL AS InformID,
			            1 AS NoID
		        FROM TSRDATA.dbo.checkercard WHERE contno = @Contno and refno = @Refno";
                returnFromCards = conn.InsertData(DbConnectionString,sqlInsertFromcards,new {
                    Contno = req.Contno,
                    Refno = req.Refno,
                });

                string sqlInsertFromcardsLog = @"INSERT INTO TSR_ONLINE_MARKETING.dbo.CheckerCard_FromCards_Log
	            (
		            [id]
                    ,[item]
                    ,[docno]
                    ,[amonth]
                    ,[ayear]
                    ,[docdate]
                    ,[refno]
                    ,[contno]
                    ,[name]
                    ,[cashcode]
                    ,[acode]
                    ,[paydate]
                    ,[senddate]
                    ,[note]
                    ,[addr1]
                    ,[addr2]
                    ,[addr3]
                    ,[addr4]
                    ,[addrzip]
                    ,[tumbon]
                    ,[lock]
                    ,[provinceid]
                    ,[districtid]
                    ,[amphurid]
                    ,[zip]
                    ,[adate]
                    ,[meetdate]
                    ,[alock]
                    ,[empid]
                    ,[status]
                    ,[date_create]
                    ,[InformID]
                    ,[NoID]
                    ,[user_code]
	            )
	            SELECT id,
			        1,
			        docno,
			        amonth,
			        ayear,
			        docdate,
			        refno,
			        contno,
			        name,
			        cashcode,
			        acode,
			        paydate,
			        senddate,
			        note,
			        addr1,
			        addr2,
			        addr3,
			        addr4,
			        addrzip,
			        tumbon,
			        lock,
			        provinceid,
			        districtid,
			        amphurid,
			        zip,
			        adate,
			        meetdate,
			        alock,
			        empid,
			        '0' AS status,
			        GETDATE(),
			        NULL AS InformID,
			        1 AS NoID,
			        'STORE'
			    FROM TSRDATA.dbo.checkercard WHERE contno = @Contno and refno = @Refno ";
                returnFromCardsLog = conn.InsertData(DbConnectionString, sqlInsertFromcardsLog, new
                {
                    Contno = req.Contno,
                    Refno = req.Refno,
                });

                if (returnFromCards.status == 200)
                {
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "ImportContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                       ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 200,
                            TYPE = "SUCCESS",
                            MESSAGE = returnFromCards.message,
                        })
                    });
                    return Ok(new
                    {
                        status = 200,
                        message = "SUCCESS",
                        description = returnFromCards.message,
                    });
                }
                else {
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "ImportContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                       ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 400,
                            TYPE = "ERROR",
                            MESSAGE = returnFromCards.message,
                        })
                    });
                    return Ok(new
                    {
                        status = 400,
                        message = "ERROR",
                        description = returnFromCards.message,
                    });
                }
            }
            catch (Exception ex) {
                conn.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "TicketChecker",
                    ControllerName = "CheckerCard",
                    FunctionName = "ImportContno",
                    JsonRequestData = new JavaScriptSerializer().Serialize(req)
                       ,
                    JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        STATUS = 500,
                        TYPE = "ERROR EXCEPTION",
                        MESSAGE = ex.Message,
                    })
                });
                return Ok(new
                {
                    status = 500,
                    message = "ERROR EXCEPTION",
                    description = ex.Message,
                });
            }
        }


        [HttpPost, Route("CheckerCard/UpdateContno")]
        public IHttpActionResult UpdateContno(RequestUpdateContno req)
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

            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON result = new ReturnJSON();
            try
            {

                if (string.IsNullOrEmpty(req.Old_Contno) || string.IsNullOrEmpty(req.New_Contno) || string.IsNullOrEmpty(req.Refno))
                { //// ถ้า parameter is null or empty
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "UpdateContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                        ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 501,
                            TYPE = "ERROR!",
                            MESSAGE = "Parameter New_Contno,Old_Contno,Refno is Null or Empty!",
                        })
                    });
                    return Ok(new
                    {
                        status = 501,
                        message = "ERROR",
                        description = "Parameter New_Contno,Old_Contno,Refno is Null or Empty!",
                    });
                }

                string sqlCheck = @"select * from [TSR_ONLINE_MARKETING].[dbo].CheckerCard_FromCards 
                WHERE Refno = @Refno AND Contno = @Contno ";
                DataTable dt = new DataTable();
                dt = conn.ExecuteDataTable(DbConnectionString,sqlCheck,new {
                    Refno = req.Refno,
                    Contno = req.Old_Contno
                });

                if (dt.Rows.Count == 0) {
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProjectName = "TicketChecker",
                        ControllerName = "CheckerCard",
                        FunctionName = "UpdateContno",
                        JsonRequestData = new JavaScriptSerializer().Serialize(req)
                    ,
                        JsonResponsData = new JavaScriptSerializer().Serialize(new
                        {
                            STATUS = 502,
                            TYPE = "ERROR",
                            MESSAGE = "Contno,Refno not found!",
                        })
                    });
                    return Ok(new
                    {
                        status = 502,
                        message = "ERROR",
                        description = "Contno,Refno not found!",
                    });
                }


               

                using (var connTrans = new SqlConnection(DbConnectionString))
                {
                    connTrans.Open();
                    // create the transaction
                    // You could use `var` instead of `SqlTransaction`
                    using (SqlTransaction tran = connTrans.BeginTransaction())
                    {
                        try
                        {
                            string sqlInsUpdateContno = @"INSERT INTO [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_UpdateContnoLog](
                                [Refno]
                                ,[Old_Contno]
                                ,[New_Contno]
                                ,[DateCreate]
                                ,[DateModify]
                                ,[UserBy]
                            ) VALUES (
                                @Refno
                                ,@Old_Contno
                                ,@New_Contno
                                ,GETDATE()
                                ,GETDATE()
                                ,@UserBy
                            )";
                            connTrans.Execute(sqlInsUpdateContno, new
                            {
                                Refno = req.Refno,
                                Old_Contno = req.Old_Contno,
                                New_Contno = req.New_Contno,
                                UserBy = req.ByUser
                            }, tran);


                            string sqlUpdate = @"
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_FromCards] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_FromCards_Log] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_Pending] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_Pending_Log] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_Master] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_Details] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                                UPDATE [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_Images] SET CONTNO = @New_Contno WHERE CONTNO = @Old_Contno;
                            ";
                            connTrans.Execute(sqlUpdate, new
                            {
                                Old_Contno = req.Old_Contno,
                                New_Contno = req.New_Contno,
                            }, tran);
                            tran.Commit();
                        }
                        catch (Exception ex) {
                            tran.Rollback();
                            conn.InsertData(DbConnectionString, sqlLogQR, new
                            {
                                ProjectName = "TicketChecker",
                                ControllerName = "CheckerCard",
                                FunctionName = "UpdateContno",
                                JsonRequestData = new JavaScriptSerializer().Serialize(req)
                                ,JsonResponsData = new JavaScriptSerializer().Serialize(new
                                {
                                    STATUS = 500,
                                    TYPE = "ERROR EXCEPTION Rollback",
                                    MESSAGE = ex.Message,
                                })
                            });
                            return Ok(new
                            {
                                Status = 500,
                                message = ex.Message.ToString(),
                                data = "",
                            });
                        }
                    }
                }

                conn.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "TicketChecker",
                    ControllerName = "CheckerCard",
                    FunctionName = "UpdateContno",
                    JsonRequestData = new JavaScriptSerializer().Serialize(req)
                    ,
                    JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        STATUS = 500,
                        TYPE = "SUCCESS",
                        MESSAGE = "แก้ไขสำเร็จ!",
                    })
                });

                return Ok(new
                {
                    Status = 200,
                    message = "Success",
                    data = "แก้ไขสำเร็จ!",
                });
            }
            catch (Exception ex)
            {
                conn.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "TicketChecker",
                    ControllerName = "CheckerCard",
                    FunctionName = "UpdateContno",
                    JsonRequestData = new JavaScriptSerializer().Serialize(req)
                       ,
                    JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        STATUS = 500,
                        TYPE = "ERROR EXCEPTION",
                        MESSAGE = ex.Message,
                    })
                });
                return Ok(new
                {
                    Status = 500,
                    message = ex.Message.ToString(),
                    data = "",
                });
            }
        }



        /// API การส่งข้อมูล กาณ์ดตรวจสอบจาก ระบบปฏิบัติการของพี่หนุ่ม ไป Toss Checker 
        [HttpGet, Route("Checked/TransferData")]
        public IHttpActionResult TransferCardToToss(string amonth,string ayear,string day,string bcode)
        {
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON result = new ReturnJSON();

            string status = "", message = "";

            string sqlCheckError = @"
                SELECT subquery.id,
			        subquery.contno,
			        subquery.refno,
			        subquery.docno,
			        subquery.docdate,
			        subquery.addr1,
			        subquery.addr2,
			        subquery.addr3,
			        subquery.addr4,
			        subquery.addrzip,
			        subquery.amonth,
			        subquery.ayear,
			        subquery.acode,
			        subquery.empid,
			        CASE   
				        WHEN subquery.StatusEmp = 0 THEN 'FALSE'
				        ELSE 'TRUE'
			        END   AS StatusEmp,
			        CASE   
				        WHEN subquery.StatusEmp = 0 THEN 'รหัส acode กับ รหัส empid ไม่ตรงกัน'
				        ELSE ''
			        END + ' ' +
			        CASE   
				        WHEN subquery.acode = '' THEN 'รหัส acode ไม่มีข้อมูล'
				        ELSE ''
			        END + ' ' +
			        CASE   
				        WHEN subquery.empid = '' THEN 'รหัส empid ไม่มีข้อมูล'
				        ELSE ''
			        END
		
			        AS StatusDescription
		        FROM (
		        SELECT c.id,
		        c.contno,
		        c.refno,
		        c.docno,
		        c.docdate,
		        c.addr1,
		        c.addr2,
		        c.addr3,
		        c.addr4,
		        c.addrzip,
		        c.amonth,
		        c.ayear,
		        ISNULL(c.acode,'') AS acode,
		        ISNULL(c.empid,'') AS empid,
		        (
			        SELECT COUNT(*) AS Num FROM TSRDATA.dbo.CAreaTripA t WHERE t.acode = c.acode AND t.empid = c.empid AND t.amonth = c.amonth 
			        AND t.ayear = c.ayear
		        ) AS StatusEmp
		        FROM TSRDATA.dbo.checkercard c
		        WHERE c.id NOT IN (
			        SELECT id FROM TSR_ONLINE_MARKETING.dbo.CheckerCard_FromCards
		        ) AND c.amonth = @Month AND c.ayear = @Year AND @Day = CASE
				    WHEN @Day <> '-'
					    THEN DAY(c.docdate)
				    ELSE @Day
			    END AND c.bcode = @bcode
		        ) AS subquery WHERE subquery.acode  = '' OR subquery.empid = '' OR subquery.StatusEmp = 0
            ";

            var resultError = conn.ExcuteStoredProcedure(DbConnectionString, sqlCheckError, new
            {
                Day = day,
                Month = amonth,
                Year = ayear,
                bcode = bcode,
            }, null, false, null, CommandType.Text);

            if (resultError.count > 0) { //// หากมี Error ไม่ดึงข้อมูล
                return Ok(new
                {
                    Status = "400",
                    message = "Fail",
                    DataError = resultError.data,
                });
            }


            string sqlCountData = @"SELECT id as Num FROM TSRDATA.dbo.checkercard WHERE 
		    amonth = @Month AND ayear = @Year AND @Day = CASE
			    WHEN @Day <> '-'
				    THEN DAY(docdate)
			    ELSE @Day
		    END AND bcode = @bcode
            ";
            var resultCountData = conn.ExcuteStoredProcedure(DbConnectionString, sqlCountData, new
            {
                Day = day,
                Month = amonth,
                Year = ayear,
                bcode = bcode,
            }, null, false, null, CommandType.Text);


            ///// call store transfer data
            string sql = @"[TSR_ONLINE_MARKETING].[dbo].[sp_CheckerCard_Transfer_HomeRed_To_Toss]";
            result = conn.ExcuteStoredProcedure(DbConnectionString, sql, new
            {
                Day = day,
                Month = amonth,
                Year = ayear,
                bcode = bcode,
            },null,false,null,CommandType.StoredProcedure);



            ///// insert log การเรียก apiนี้
            string sqlLog = @"INSERT INTO [TSR_ONLINE_MARKETING].[dbo].[CheckerCard_Transaction_Log] ([Status]
            ,[Message]) VALUES (@Status,@Message)";
            conn.InsertData(DbConnectionString, sqlLog,new {
                Status = result.status,
                Message = result.message,
            });

            //// ถ้าข้อมูลถูกต้อง ให้แจ้งเตือน
            if (result.status == 200) {
                using (var ws = new WebSocket("ws://toss.thiensurat.co.th:3002"))
                {
                    ws.OnMessage += (sender, e) =>
                      Console.WriteLine("Laputa says: " + e.Data);
                    ws.Connect();
                    //ws.se
                    ws.Send(JsonConvert.SerializeObject(new
                    {
                        project = "CHECKER",
                        MessageDetails = "แจ้งเตือน การส่งข้อมูลการ์ดตรวจสอบ",
                        MessageHeader = "มีการส่งข้อมูลการ์ดที่ต้องไปตรวจสอบเข้ามาในระบบ",
                        type = "TranferData",
                    }));
                    //Console.ReadKey(true);
                }
            }







            return Ok(new
            {
                Status = result.status,
                message = result.message,
                DataError = result.data,
                CountSuccess = resultCountData.count,
                //Desc = " acode กับ empid เทียบจาก ตาราง TSRDATA.dbo.CAreaTripA เงื่อนไข ใช้ amonth กับ ayear เป็นตัวเชค ปักปี"
            });
        }



      
        [HttpGet, Route("Checked/test")]
        public IHttpActionResult test()
        {
            return Ok(new
            {
                Status = 200,
                message = "Success",
                data = "ยกเลิกสำเร็จ!",
            });

        }


        /// API ยกเลิกสัญญาในระบบตรวจสอบ 
        [HttpPost, Route("Checked/ContractCancel")]
        public IHttpActionResult ContractCancel(RequestCheckedContractCancel request)
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

            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON result = new ReturnJSON();
            try
            {
              
                string sql = @"[TSR_ONLINE_MARKETING].[dbo].[sp_CheckerCard_contract_cancel]";
                result = conn.ExcuteStoredProcedure(DbConnectionString, sql, new
                {
                    Contno = request.Contno,
                    FnMonth = request.FnMonth,
                    FnYear = request.FnYear,
                }, null, false, null, CommandType.StoredProcedure);

                conn.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "TicketChecker",
                    ControllerName = "CheckerCard",
                    FunctionName = "ContractCancel",
                    JsonRequestData = new JavaScriptSerializer().Serialize(request)
                    ,JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        STATUS = 500,
                        TYPE = "SUCCESS",
                        MESSAGE = "ยกเลิกสำเร็จ!",
                    })
                });

                return Ok(new
                {
                    Status = 200,
                    message = "Success",
                    data = "ยกเลิกสำเร็จ!",
                });
            }
            catch (Exception ex)
            {
                conn.InsertData(DbConnectionString, sqlLogQR, new
                {
                    ProjectName = "TicketChecker",
                    ControllerName = "CheckerCard",
                    FunctionName = "ContractCancel",
                    JsonRequestData = new JavaScriptSerializer().Serialize(request)
                       ,
                    JsonResponsData = new JavaScriptSerializer().Serialize(new
                    {
                        STATUS = 500,
                        TYPE = "ERROR EXCEPTION",
                        MESSAGE = ex.Message,
                    })
                });
                return Ok(new
                {
                    Status = 500,
                    message = ex.Message.ToString(),
                    data = "",
                });
            }

        }

    }

    public class RequestCheckedContractCancel {
        public string Contno { get; set; }
        public string FnYear { get; set; }
        public string FnMonth { get; set; }
        public string IPAddress { get; set; }
        public string ComputerName { get; set; }
        public string ByUser { get; set; }
    }


    public class RequestImportContno {
        public string Contno { get; set; }
        public string Refno { get; set; }
        public string IPAddress { get; set; }
        public string ComputerName { get; set; }
        public string ByUser { get; set; }
    }


    public class RequestUpdateContno {

        public string Old_Contno { get; set; }
        public string New_Contno { get; set; }
        public string Refno { get; set; }
        public string IPAddress { get; set; }
        public string ComputerName { get; set; }
        public string ByUser { get; set; }
    }
}