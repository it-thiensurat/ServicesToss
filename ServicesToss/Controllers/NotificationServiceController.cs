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
    public class NotificationServiceController : ApiController
    {
        // GET: NotificationService
        [HttpGet, Route("api/NotificationService")]
        public IHttpActionResult Index()
        {
            return Ok(new
            {
                status = "200",
                message = "hello world",
            });
        }

        /// <summary>
        /// get list customer สารกรอง
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("api/NotificationService/GetCustomerWaterFilter")]
        public IHttpActionResult GetCustomerWaterFilter()
        {
            string DbConnectionString = WebConfigurationManager.ConnectionStrings["TSR-SQL-02"].ConnectionString;
            try
            {
                ConnectDB conn = new ConnectDB();
                string sql = @"SELECT 
	                CONVERT(VARCHAR(10),dm.EffDate,121) as EffDate,
	                CAST(YEAR(GETDATE()) AS VARCHAR(4)) + '-' + CONVERT(VARCHAR(5),dm.EffDate,10) AS ExpiredDate,
	                dm.Refno,
	                dm.CONTNO,
	                dm.PrefixName + ' ' + dm.CustomerName AS CustomerName,
	                dm.ProductSerial,
	                dm.ProductName,
	                da.TelMobile,
	                da.TelHome,
	                da.TelOffice
                FROM TSR_Application.dbo.DebtorAnalyze_Master dm 
                LEFT JOIN TSR_Application.dbo.DebtorAnalyze_Address da ON dm.CONTNO = da.CONTNO AND da.AddressTypeCode = 'AddressIDCard'
                WHERE dm.ProductType in ('สารกรอง','เครื่องกรองน้ำ') 
                AND MONTH(EffDate) = MONTH(GETDATE()) AND DAY(dm.EffDate) = DAY(GETDATE()) AND YEAR(EffDate) != YEAR(GETDATE())";
                var result = conn.ExcuteStoredProcedure(DbConnectionString, sql, null, null, false, null, CommandType.Text);
                return Ok(new
                {
                    STATUS = 200,
                    MESSAGE = "Successful",
                    DATA = result.data,
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    STATUS = 500,
                    MESSAGE = ex.Message.ToString(),
                    DATA = "",
                });
            }
        }





    }
}