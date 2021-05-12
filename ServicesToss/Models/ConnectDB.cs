using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Web;
using System.Web.Configuration;
using Dapper;
namespace ServicesToss.Models
{
    public class ConnectDB
    {
        public string sqlLogServiceToss = @"INSERT INTO [TSR_ONLINE_MARKETING].[dbo].[ServiceToss_Log] (
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
        )"; // string sql insert .og to table tsr_online_market.dbo.ServiceToss_Log

        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }





        public ReturnJSON InsertData(string DbConnectionString, string sqlText, object sqlParams = null, IDbTransaction transaction = null, bool buffer = false, int? timeOut = null, CommandType? commandType = null)
        {
            {
                ReturnJSON returnJson = new ReturnJSON();
                using (var conn = new SqlConnection(DbConnectionString))
                {
                    try
                    {
                        conn.Open();
                        var result = conn.Execute(sqlText, sqlParams, transaction, timeOut, commandType);
                        if (result == 1)
                        {
                            returnJson.status = 200;
                            returnJson.message = "SUCCESS";
                            returnJson.data = result;
                        }
                        else
                        {
                            returnJson.status = 201;
                            returnJson.message = "ERROR";
                        }

                    }
                    catch (Exception ex)
                    {
                        returnJson.status = 500;
                        returnJson.message = ex.Message;
                    }
                    return returnJson;
                }
            }

        }

        public DataTable ExecuteDataTable(string DbConnectionString, string commandText, object commandParameters)
        {
            using (var connection = new SqlConnection(DbConnectionString))
            {
                connection.Open();
                using (var reader = connection.ExecuteReader(commandText, commandParameters))
                {
                    using (var dt = new DataTable())
                    {
                        dt.Load(reader);
                        return dt;
                    }
                }
            }
        }

        public ReturnJSON ExcuteStoredProcedure(string DbConnectionString, string sqlText, object sqlParams = null, IDbTransaction transaction = null, bool buffer = true, int? timeOut = null, CommandType? commandType = null)
        {
            {
                ReturnJSON returnJson = new ReturnJSON();
                using (var conn = new SqlConnection(DbConnectionString))
                {
                    try
                    {
                        conn.Open();
                        var result = conn.Query(sqlText, sqlParams, transaction, false,null, commandType).ToList();

                        returnJson.status = 200;
                        returnJson.message = "SUCCESS";
                        returnJson.data = result;
                        returnJson.count = result.Count;
                        //if (result == 1)
                        //{
                        //    returnJson.status = 200;
                        //    returnJson.message = "SUCCESS";
                        //    returnJson.data = result;
                        //}
                        //else
                        //{
                        //    returnJson.status = 201;
                        //    returnJson.message = "ERROR";
                        //}

                    }
                    catch (Exception ex)
                    {
                        returnJson.status = 500;
                        returnJson.message = ex.Message;
                    }
                    return returnJson;
                }
            }

        }


    }


    public class ReturnJSON
    {
        public int status { get; set; }
        public string message { get; set; }
        public object data { get; set; }
        public int count { get; set; }
    }




}