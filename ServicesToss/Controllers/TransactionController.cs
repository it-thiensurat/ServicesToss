using System;
using System.Collections.Generic;
using System.Data;
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
    public class TransactionController : ApiController
    {

        // GET api/<controller>/5

        [HttpGet, Route("api/RequestExample")]
        public IHttpActionResult Example()
        {
            //ConnectDB conn = new ConnectDB();
            //string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            ///////// call store transfer data
            //string sqlStore = @"[INETSQL_PROD].TSRData_Source.dbo.SP_TSSM_CreateReceiptTransfer";
            //var stmt = conn.ExcuteStoredProcedure(DbConnectionString, sqlStore, new
            //{
            //    Empid = "A27367", //รหัสพนักงาน ฟิกไว้
            //    Contno = "30104438", // เลขที่สัญญา
            //    ContractReferenceNo = "620042560", //เลขที่อ้างอิง
            //    PayTran = (int)float.Parse("1.00"), /// จำนวนเงิน
            //    Ways = "TSR QR Payment", /// ช่องทาง
            //    DateTransfer = "2019-08-06 11:40", // วันที่จ่าย
            //}, null, false, null, CommandType.StoredProcedure);
            //if (stmt.status == 200)
            //{

            //}
            //int x = (int)float.Parse("1.00");
            return Ok(new
            {
                BankRef = "201709191212120000001",
                ResCode = "000",
                ResDesc = "Success",
                TransDate = "20160101080000"
            });
        }

        [HttpPost,Route("api/RequestTransaction")]
        public IHttpActionResult RequestTransaction(TMB_Request request) {
            TMB_Respons respons = new TMB_Respons();
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-UAT"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON result = new ReturnJSON();

            try
            {
                DateTime TransDate = DateTime.ParseExact(request.TransDate.ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                string sqlLog = @"INSERT INTO [TSR_DB1].dbo.TRANSECTION_PAYMENT_LOG (date_create,exception) VALUES (GETDATE(),@ex)";
                conn.InsertData(DbConnectionString, sqlLog,new {
                    ex = JsonConvert.SerializeObject(request)
                });
                string sqlIns = @"INSERT INTO [TSR_DB1].[dbo].[TRANSECTION_PAYMENT] (
                [BankName]
                ,[BankRef]
                ,[BillerNo]
                ,[Ref1]
                ,[Ref2]
                ,[QRId]
                ,[PayerName]
                ,[PayerBank]
                ,[Filler]
                ,[Amount]
                ,[ResultCode]
                ,[ResultDesc]
                ,[TransDate]
                ,[IpAddress]
                ,[ComputerName]
                ,[Date_Create]
                ,[Date_Modify]
                ,[Create_By]
            ) VALUES(
                @BankName
                ,@BankRef
                ,@BillerNo
                ,@Ref1
                ,@Ref2
                ,@QRId
                ,@PayerName
                ,@PayerBank
                ,@Filler
                ,@Amount
                ,@ResultCode
                ,@ResultDesc
                ,@TransDate
                ,@IpAddress
                ,@ComputerName
                ,GETDATE()
                ,GETDATE()
                ,'SYSTEM'
            )";
                string ipAddress = conn.GetLocalIPAddress();
                string strComputerName = Environment.MachineName.ToString();

                result = conn.InsertData(DbConnectionString, sqlIns, new
                {
                    BankName = "TMB"
                    ,
                    BankRef = request.BankRef.ToString()
                    ,
                    BillerNo = request.BillerNo.ToString()
                    ,
                    Ref1 = request.Ref1.ToString()
                    ,
                    Ref2 = request.Ref2.ToString()
                    ,
                    QRId = request.QRId.ToString()
                    ,
                    PayerName = request.PayerName.ToString()
                    ,
                    PayerBank = request.PayerBank.ToString()
                    ,
                    Filler = request.Filler.ToString()
                    ,
                    Amount = request.Amount.ToString()
                    ,
                    ResultCode = request.ResultCode.ToString()
                    ,
                    ResultDesc = request.ResultDesc.ToString()
                    ,
                    TransDate = TransDate
                    ,
                    IpAddress = ipAddress.ToString()
                    ,
                    ComputerName = strComputerName.ToString()
                });

                respons.BankRef = request.BankRef.ToString();
                respons.ResCode = result.status == 200 ? "000" : "999";
                respons.ResDesc = result.message;
                respons.TransDate = request.TransDate.ToString();


                TMB_Verify verify = new TMB_Verify();
                verify.BillerNo = request.BillerNo.ToString();
                verify.QRId = request.QRId.ToString();
                var respons_verify = VerifyTransactionTMB(verify);
                if (respons_verify.status == 200)
                {
                    /// insert verify success
                    string sqlInsVerify = @"INSERT INTO [TSR_DB1].[dbo].[TRANSACTION_PAYMENT_VERIFY_SLIP] (
                    [BankRef]
                    ,[responseCode]
                    ,[transAmount]
                    ,[billReference1]
                    ,[billReference2]
                    ,[date_create]
                    ,[user_code]                
                )VALUES(
                    @BankRef
                    ,@responseCode
                    ,@transAmount
                    ,@billReference1
                    ,@billReference2
                    ,GETDATE()
                    ,@user_code   
                )
                ";
                    TMB_Verify_Respons res = new TMB_Verify_Respons();
                    res = JsonConvert.DeserializeObject<TMB_Verify_Respons>(respons_verify.data.ToString());
                    conn.InsertData(DbConnectionString, sqlInsVerify, new
                    {
                        BankRef = request.BankRef.ToString(),
                        responseCode = res.responseCode,
                        transAmount = res.transAmount,
                        billReference1 = res.billReference1,
                        billReference2 = res.billReference2,
                        user_code = "SYSTEM"
                    });
                }

                //////////////////////////////////////////////////////////////////////////////// 
                /// call store receipt

                string sqlLogQR = @"INSERT [TSR_DB1].[dbo].[QR_PAYMENT_RECEIPT_LOG] (
                    ProductSerial
                    ,[Contno]
                    ,[Refno]
                    ,[Amount]
                    ,[DateTransfer]
                    ,[CreateReceipt_Status]
                    ,[CreateReceipt_Message]
                    ,[SendSMS_Status]
                    ,[SendSMS_Message]
                ) VALUES (
                    @ProductSerial
                    ,@Contno
                    ,@Refno
                    ,@Amount
                    ,@DateTransfer
                    ,@CreateReceipt_Status
                    ,@CreateReceipt_Message
                    ,@SendSMS_Status
                    ,@SendSMS_Message
                )";

                /* string getContno = @"SELECT 
                 dm.Refno
                 ,dm.CONTNO
                 ,dm.CashEmpID
                 ,CAST(dm.PayLastPeriod AS INT) AS PayLastPeriod
                 ,dm.AllPeriods
                 ,dm.ProductName
                 ,da.TelMobile
                 ,da.TelHome
                 ,da.TelOffice
                 FROM TSR_Application.dbo.DebtorAnalyze_Master dm
                 LEFT JOIN TSR_Application.dbo.DebtorAnalyze_Address da ON da.CONTNO = dm.CONTNO AND da.Refno = dm.Refno AND da.AddressTypeCode = 'AddressInstall'
                 WHERE dm.ProductSerial =  @ProductSerial ";*/

                string getContno = @"SELECT 
                dm.Refno
                ,dm.CONTNO
                ,dm.CashEmpID
                ,CAST(dm.PayLastPeriod AS INT) AS PayLastPeriod
                ,dm.AllPeriods
                ,dm.ProductName
                ,da.TelMobile
                ,da.TelHome
                ,da.TelOffice
                FROM TSR_Application.dbo.DebtorAnalyze_Master dm WITH(NOLOCK)
                LEFT JOIN TSR_Application.dbo.DebtorAnalyze_Address da WITH(NOLOCK) ON da.CONTNO = dm.CONTNO AND da.Refno = dm.Refno AND da.AddressTypeCode = 'AddressInstall'
                WHERE dm.ProductSerial =  @ProductSerial
                UNION ALL
                SELECT C.ContractReferenceNo AS refno
                ,C.CONTNO
                ,C.SaleEmployeeCode AS CashEmpID
                ,1 AS PayLastPeriod
                ,'0' AS AllPeriods
                ,'-' AS ProductName
                ,A.TelMobile
                ,A.TelHome
                ,A.TelOffice
                FROM Bighead_Mobile.dbo.Contract AS C WITH(NOLOCK)
                LEFT JOIN Bighead_Mobile.dbo.Address AS A WITH(NOLOCK) ON A.RefNo = C.Refno AND A.AddressTypeCode = 'AddressInstall'
                WHERE CAST(C.EFFDATE AS DATE) = CAST(GETDATE() AS DATE) AND C.isActive = 1 AND C.ProductSerialNumber = @ProductSerial ";

                DataTable dt = new DataTable();
                dt = conn.ExecuteDataTable(DbConnectionString, getContno, new
                {
                    ProductSerial = request.Ref1.ToString()
                });
                if (dt.Rows.Count > 0)
                {
                    string TelMobile = dt.Rows[0]["TelMobile"].ToString().Replace("-","");
                    string TelHome = dt.Rows[0]["TelHome"].ToString();
                    string PayLastPeriod = dt.Rows[0]["PayLastPeriod"].ToString();
                    string AllPeriods = dt.Rows[0]["AllPeriods"].ToString();
                    string CONTNO = dt.Rows[0]["CONTNO"].ToString();
                    string Refno = dt.Rows[0]["Refno"].ToString();
                    string CashEmpID = dt.Rows[0]["CashEmpID"].ToString();

                    /////// call store transfer data
                    string sqlStore = @"TSRData_Source.dbo.SP_TSSM_CreateReceiptTransfer";
                    var stmt = conn.ExcuteStoredProcedure(DbConnectionString, sqlStore, new
                    {
                        Empid = "X00033", //รหัสพนักงาน ฟิกไว้
                        Contno = CONTNO, // เลขที่สัญญา
                        ContractReferenceNo = Refno, //เลขที่อ้างอิง
                        PayTran = (int)float.Parse(request.Amount), /// จำนวนเงิน
                        Ways = "TSR QR Payment", /// ช่องทาง
                        DateTransfer = request.TransDate.ToString(), // วันที่จ่าย
                        PayDate = request.TransDate.ToString(), // วันที่จ่าย
                    }, null, false, null, CommandType.StoredProcedure);
                    if (stmt.status == 200)
                    {
                        /// send sms
                        if (TelMobile != null || TelMobile != "")
                        {
                            //string url = "https://toss.thiensurat.co.th/onlineReceipt/report.php?contno=" + CONTNO;
                            string url = "https://toss.thiensurat.co.th/onlineReceipt/report.php";
                            string Message = @"ขอบคุณที่ชำระค่างวด จำนวน "+ request.Amount + " บ. เพิ่มเติม";
                            //string apiSendSMS = @"https://toss.thiensurat.co.th/sendsms/SendsmsQr.php?telno=0819588128"+ @"&msg=" + Message + "&sender=TSR&owner=System&para=" + CONTNO;
                            string apiSendSMS = @"https://toss.thiensurat.co.th/sendsms/SendsmsQr.php?telno=" + TelMobile + @"&msg=" + Message + "&sender=TSR&owner=System&para="+ CONTNO;
                            var responsSMS = SendSMS(apiSendSMS);
                            conn.InsertData(DbConnectionString, sqlLogQR, new
                            {
                                ProductSerial = request.Ref1.ToString()
                                ,
                                Contno = CONTNO
                                ,
                                Refno = Refno
                                ,
                                Amount = request.Amount
                                ,
                                DateTransfer = TransDate
                                ,
                                CreateReceipt_Status = stmt.status
                                ,
                                CreateReceipt_Message = stmt.message
                                ,
                                SendSMS_Status = responsSMS.status
                                ,
                                SendSMS_Message = responsSMS.message
                            });
                        }
                        else
                        {
                            conn.InsertData(DbConnectionString, sqlLogQR, new
                            {
                                ProductSerial = request.Ref1.ToString()
                                ,
                                Contno = CONTNO
                                ,
                                Refno = Refno
                                ,
                                Amount = request.Amount
                                ,
                                DateTransfer = TransDate
                                ,
                                CreateReceipt_Status = stmt.status
                                ,
                                CreateReceipt_Message = stmt.message
                                ,
                                SendSMS_Status = "404"
                                ,
                                SendSMS_Message = "ไม่พบเบอร์โทรศัพท์ลูกค้า"
                            });
                        }

                        ///// ส่งการแจ้งเตือนไปยัง APP TICKET CHECKER 
                        string TicketURL = @"http://app.thiensurat.co.th/assanee/api_sale_all_problem_from_cedit_by_db_kiw/firebase_nontification_credit_from_web/index.php?EmpID="+ CashEmpID + @"&contno=" + CONTNO;
                        var responsTicket = SendSMS(TicketURL);
                    }
                    else
                    {

                        //// เรียก store ไม่สำเร็จ
                        conn.InsertData(DbConnectionString, sqlLogQR, new
                        {
                            ProductSerial = request.Ref1.ToString()
                            ,
                            Contno = CONTNO
                            ,
                            Refno = Refno
                            ,
                            Amount = request.Amount
                            ,
                            DateTransfer = TransDate
                            ,
                            CreateReceipt_Status = stmt.status
                            ,
                            CreateReceipt_Message = stmt.message
                            ,
                            SendSMS_Status = ""
                            ,
                            SendSMS_Message = ""
                        });
                    }
                }
                else
                {
                    //// ไม่พบเลขที่สัญญา
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProductSerial = request.Ref1.ToString()
                        ,
                        Contno = ""
                        ,
                        Refno = ""
                        ,
                        Amount = request.Amount
                        ,
                        DateTransfer = TransDate
                        ,
                        CreateReceipt_Status = "404"
                        ,
                        CreateReceipt_Message = "ไม่พบเลขที่สัญญา"
                        ,
                        SendSMS_Status = ""
                        ,
                        SendSMS_Message = ""
                    });
                }


                using (var ws = new WebSocket("ws://toss.thiensurat.co.th:3002"))
                {
                    ws.OnMessage += (sender, e) =>
                      Console.WriteLine("Laputa says: " + e.Data);
                    ws.Connect();
                    //ws.se
                    ws.Send(JsonConvert.SerializeObject(new
                    {
                        Project = "PaymentTransaction",
                        Type = "Request",
                        Data = request
                    }));
                    //Console.ReadKey(true);
                }
            }
            catch (Exception ex) {
                conn.InsertData(DbConnectionString,
                @"INSERT INTO TSR_DB1.dbo.TRANSECTION_PAYMENT_LOG (
                    date_create,
                    exception
                )
                VALUES (
                    GETDATE(),
                    @EX
                )
                ", new
                {
                    EX = ex.Message.ToString()
                });
            }

            return Ok(respons);
        }


        private ReturnJSON VerifyTransactionTMB(TMB_Verify requests) {
            ReturnJSON result = new ReturnJSON();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.datagateway.tmbbank.com:9643/VerifySlip/dgw/Verify/QRPayment/TSR");
                request.Method = "POST";
                request.Accept = "application/JSON";
                request.ContentType = "application/JSON; charset=utf-8";
                System.Net.ServicePointManager.ServerCertificateValidationCallback =
    ((sender, certificate, chain, sslPolicyErrors) => true);
                StreamWriter reqStream = new StreamWriter(request.GetRequestStream());
                reqStream.Write(JsonConvert.SerializeObject(requests));
                reqStream.Close();
                HttpWebResponse objResponse = (HttpWebResponse)request.GetResponse();
                StreamReader resStream = new StreamReader(objResponse.GetResponseStream());
                var respons = resStream.ReadToEnd();
                result.status = 200;
                result.message = "Success";
                result.data = respons;
                resStream.Close();
            }
            catch (Exception ex)
            {
                result.status = 400;
                result.message = ex.Message.ToString();
            }
            return result;
        }


        private ReturnJSON SendSMS(string url) {
            ReturnJSON result = new ReturnJSON();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    reader.ReadToEnd();
                }
                result.status = 200;
                result.message = "Success";
                //result.data = respons;
                
            }
            catch (Exception ex)
            {
                result.status = 400;
                result.message = ex.Message.ToString();
            }
            return result;
        }

        [HttpPost, Route("api/UAT/RequestTransaction")]
        public IHttpActionResult RequestTransactionUAT(TMB_Request request) {
            TMB_Respons respons = new TMB_Respons();
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-UAT"].ConnectionString;
            ConnectDB conn = new ConnectDB();
            ReturnJSON result = new ReturnJSON();

            try
            {
                DateTime TransDate = DateTime.ParseExact(request.TransDate.ToString(), "yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                string sqlLog = @"INSERT INTO [TSR_DB1].dbo.TRANSECTION_PAYMENT_LOG (date_create,exception) VALUES (GETDATE(),@ex)";
                conn.InsertData(DbConnectionString, sqlLog,new {
                    ex = JsonConvert.SerializeObject(request)
                });
                string sqlIns = @"INSERT INTO [TSR_DB1].[dbo].[TRANSECTION_PAYMENT] (
                [BankName]
                ,[BankRef]
                ,[BillerNo]
                ,[Ref1]
                ,[Ref2]
                ,[QRId]
                ,[PayerName]
                ,[PayerBank]
                ,[Filler]
                ,[Amount]
                ,[ResultCode]
                ,[ResultDesc]
                ,[TransDate]
                ,[IpAddress]
                ,[ComputerName]
                ,[Date_Create]
                ,[Date_Modify]
                ,[Create_By]
            ) VALUES(
                @BankName
                ,@BankRef
                ,@BillerNo
                ,@Ref1
                ,@Ref2
                ,@QRId
                ,@PayerName
                ,@PayerBank
                ,@Filler
                ,@Amount
                ,@ResultCode
                ,@ResultDesc
                ,@TransDate
                ,@IpAddress
                ,@ComputerName
                ,GETDATE()
                ,GETDATE()
                ,'SYSTEM'
            )";
                string ipAddress = conn.GetLocalIPAddress();
                string strComputerName = Environment.MachineName.ToString();

                result = conn.InsertData(DbConnectionString, sqlIns, new
                {
                    BankName = "TMB"
                    ,
                    BankRef = request.BankRef.ToString()
                    ,
                    BillerNo = request.BillerNo.ToString()
                    ,
                    Ref1 = request.Ref1.ToString()
                    ,
                    Ref2 = request.Ref2.ToString()
                    ,
                    QRId = request.QRId.ToString()
                    ,
                    PayerName = request.PayerName.ToString()
                    ,
                    PayerBank = request.PayerBank.ToString()
                    ,
                    Filler = request.Filler.ToString()
                    ,
                    Amount = request.Amount.ToString()
                    ,
                    ResultCode = request.ResultCode.ToString()
                    ,
                    ResultDesc = request.ResultDesc.ToString()
                    ,
                    TransDate = TransDate
                    ,
                    IpAddress = ipAddress.ToString()
                    ,
                    ComputerName = strComputerName.ToString()
                });

                respons.BankRef = request.BankRef.ToString();
                respons.ResCode = result.status == 200 ? "000" : "999";
                respons.ResDesc = result.message;
                respons.TransDate = request.TransDate.ToString();


                /// call store receipt

                string sqlLogQR = @"INSERT [TSR_DB1].[dbo].[QR_PAYMENT_RECEIPT_LOG] (
                    ProductSerial
                    ,[Contno]
                    ,[Refno]
                    ,[Amount]
                    ,[DateTransfer]
                    ,[CreateReceipt_Status]
                    ,[CreateReceipt_Message]
                    ,[SendSMS_Status]
                    ,[SendSMS_Message]
                ) VALUES (
                    @ProductSerial
                    ,@Contno
                    ,@Refno
                    ,@Amount
                    ,@DateTransfer
                    ,@CreateReceipt_Status
                    ,@CreateReceipt_Message
                    ,@SendSMS_Status
                    ,@SendSMS_Message
                )";

                string getContno = @"SELECT 
                dm.Refno
                ,dm.CONTNO
                ,dm.CashEmpID
                ,CAST(dm.PayLastPeriod AS INT) AS PayLastPeriod
                ,dm.AllPeriods
                ,dm.ProductName
                ,da.TelMobile
                ,da.TelHome
                ,da.TelOffice
                FROM TSR_Application.dbo.DebtorAnalyze_Master dm
                LEFT JOIN TSR_Application.dbo.DebtorAnalyze_Address da ON da.CONTNO = dm.CONTNO AND da.Refno = dm.Refno AND da.AddressTypeCode = 'AddressInstall'
                WHERE dm.ProductSerial =  @ProductSerial ";
                DataTable dt = new DataTable();
                dt = conn.ExecuteDataTable(DbConnectionString, getContno, new
                {
                    ProductSerial = request.Ref1.ToString()
                });
                if (dt.Rows.Count > 0)
                {
                    string TelMobile = dt.Rows[0]["TelMobile"].ToString().Replace("-","");
                    string TelHome = dt.Rows[0]["TelHome"].ToString();
                    string PayLastPeriod = dt.Rows[0]["PayLastPeriod"].ToString();
                    string AllPeriods = dt.Rows[0]["AllPeriods"].ToString();
                    string CONTNO = dt.Rows[0]["CONTNO"].ToString();
                    string Refno = dt.Rows[0]["Refno"].ToString();
                    string CashEmpID = dt.Rows[0]["CashEmpID"].ToString();

                    /////// call store transfer data
                    string sqlStore = @"TSRData_Source.dbo.SP_TSSM_CreateReceiptTransfer";
                    var stmt = conn.ExcuteStoredProcedure(DbConnectionString, sqlStore, new
                    {
                        Empid = "A27367", //รหัสพนักงาน ฟิกไว้
                        Contno = CONTNO, // เลขที่สัญญา
                        ContractReferenceNo = Refno, //เลขที่อ้างอิง
                        PayTran = (int)float.Parse(request.Amount), /// จำนวนเงิน
                        Ways = "TSR QR Payment", /// ช่องทาง
                        DateTransfer = request.TransDate.ToString(), // วันที่จ่าย
                        PayDate = request.TransDate.ToString(), // วันที่จ่าย
                    }, null, false, null, CommandType.StoredProcedure);
                    if (stmt.status == 200)
                    {
                        conn.InsertData(DbConnectionString, sqlLogQR, new
                        {
                            ProductSerial = request.Ref1.ToString()
                                ,
                            Contno = CONTNO
                                ,
                            Refno = Refno
                                ,
                            Amount = request.Amount
                                ,
                            DateTransfer = TransDate
                                ,
                            CreateReceipt_Status = stmt.status
                                ,
                            CreateReceipt_Message = stmt.message
                                ,
                            SendSMS_Status = "500"
                                ,
                            SendSMS_Message = "ไม่พบเบอร์โทรศัพท์ลูกค้า (ปิดคำสั่งไว้)"
                        });
                    }
                    else
                    {
                       
                        //// เรียก store ไม่สำเร็จ
                        conn.InsertData(DbConnectionString, sqlLogQR, new
                        {
                            ProductSerial = request.Ref1.ToString()
                            ,
                            Contno = CONTNO
                            ,
                            Refno = Refno
                            ,
                            Amount = request.Amount
                            ,
                            DateTransfer = TransDate
                            ,
                            CreateReceipt_Status = stmt.status
                            ,
                            CreateReceipt_Message = stmt.message
                            ,
                            SendSMS_Status = ""
                            ,
                            SendSMS_Message = ""
                        });
                        return Ok(new
                        {
                            status = "Error เรียก store ไม่สำเร็จ",
                            message = stmt.message,
                        });
                    }
                }
                else
                {
                    //// ไม่พบเลขที่สัญญา
                    conn.InsertData(DbConnectionString, sqlLogQR, new
                    {
                        ProductSerial = request.Ref1.ToString()
                        ,
                        Contno = ""
                        ,
                        Refno = ""
                        ,
                        Amount = request.Amount
                        ,
                        DateTransfer = TransDate
                        ,
                        CreateReceipt_Status = "404"
                        ,
                        CreateReceipt_Message = "ไม่พบเลขที่สัญญา"
                        ,
                        SendSMS_Status = ""
                        ,
                        SendSMS_Message = ""
                    });
                }

            }
            catch (Exception ex) {
                conn.InsertData(DbConnectionString,
                @"INSERT INTO TSR_DB1.dbo.TRANSECTION_PAYMENT_LOG (
                    date_create,
                    exception
                )
                VALUES (
                    GETDATE(),
                    @EX
                )
                ", new
                {
                    EX = ex.Message.ToString()
                });
                return Ok(new
                {
                    status = "Error Exception",
                    message = ex.Message.ToString(),
                });
            }

            return Ok(new {
                status = "Success",
                message = "Successful",
            });
        }
    }


    public class TMB_Request {
        public string BankRef { get; set; }
        public string BillerNo { get; set; }
        public string Ref1 { get; set; }
        public string Ref2 { get; set; }
        public string QRId { get; set; }
        public string PayerName { get; set; }
        public string PayerBank { get; set; }
        public string Filler { get; set; }
        public string Amount { get; set; }
        public string ResultCode { get; set; }
        public string ResultDesc { get; set; }
        public string TransDate { get; set; }
    }

    public class TMB_Respons {
        public string BankRef { get; set; }
        public string ResCode { get; set; }
        public string ResDesc { get; set; }
        public string TransDate { get; set; }
    }

    public class TMB_Verify {
        public string BillerNo { get; set; }
        public string QRId { get; set; }
    }

    public class TMB_Verify_Respons
    {
        public string responseCode { get; set; }
        public string transAmount { get; set; }
        public string billReference1 { get; set; }
        public string billReference2 { get; set; }
    }



}