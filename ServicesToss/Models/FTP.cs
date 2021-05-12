using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;

namespace ServicesToss.Models
{
    class ftp
    {
        public bool result { get; set; }
        public string message { get; set; }
        public string url { get; set; }
        public ftp FtpUpFile(string base64, string upfilename,string DbConnectionString)
        {
            base64 = base64.Replace("data:image/png;base64,", String.Empty);
            base64 = base64.Replace("data:image/jpeg;base64,", String.Empty);
            byte[] filebytes = Convert.FromBase64String(base64);

            ftp ff = new ftp();
            string f_year = "", f_month = "";
            ConnectDB conn = new ConnectDB();
            string sqlGenYYMM = "Select (CONVERT([varchar],datepart(year,GetDate()),(0))) As year_txt,(right('00'+CONVERT([varchar],datepart(month,GetDate()),(0)),(2))) As month_txt";
            DataTable dt = conn.ExecuteDataTable(DbConnectionString,sqlGenYYMM,null);
            if (dt.Rows.Count > 0)
            {
                f_year = dt.Rows[0]["year_txt"].ToString().Trim();
                f_month = dt.Rows[0]["month_txt"].ToString().Trim();
            }

            string userName = "fileshare01@thiensurat.com";
            string passWord = "CX8Q2Z7wO";
            string MParth = "ftp://ftp.thiensurat.com/Ticket/";
            string DirPath = MParth + "/" + f_year;
            string urlpath = "http://thiensurat.com/fileshare01/Ticket/" + f_year;

            if (CreateFTPDirectory(DirPath, userName, passWord))
            {
                string subDirPath = DirPath + "/" + f_month;
                urlpath = urlpath + "/" + f_month;
                if (CreateFTPDirectory(subDirPath, userName, passWord))
                {
                    string upPath = subDirPath + "/" + upfilename + ".jpg";
                    urlpath = urlpath + "/" + upfilename + ".jpg";
                    if (FtpFileExists(upPath, userName, passWord))
                    {
                        ff.result = true;
                        ff.message = "File found.";
                    }
                    else
                    {
                        try
                        {
                            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(upPath);
                            request.Credentials = new NetworkCredential(userName, passWord);

                            request.KeepAlive = true;
                            request.UseBinary = true;
                            request.Method = WebRequestMethods.Ftp.UploadFile;
                            //double fileSizeKB = imgfile.Length / 1024;
                            Image image = byteArrayToImage(filebytes);
                            //if (fileSizeKB > 200)
                            //{
                            //    Bitmap newImage = ResizeImg(img, (img.Width / 2), (img.Height / 2));
                            //    image = newImage;
                            //}

                            MemoryStream ms = new MemoryStream();
                            image.Save(ms, ImageFormat.Jpeg);
                            byte[] buffer = ms.ToArray();

                            request.ContentLength = ms.Length;

                            Stream ftpstream = request.GetRequestStream();
                            ftpstream.Write(buffer, 0, buffer.Length);
                            ftpstream.Close();

                            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                            ff.message = string.Format("Upload File Complete, status {0}", response.StatusDescription);
                            //Console.WriteLine("Upload File Complete, status {0}", response.StatusDescription);

                            response.Close();

                            ms.Close();
                            ff.result = true;
                            ff.url = urlpath;
                            image.Dispose();
                        }
                        catch (Exception ex)
                        {
                            ff.result = false;
                            ff.message = ex.Message + "//" + upPath;
                        }
                    }
                }
            }

            //img.Dispose();

            return ff;
        }

        public Image byteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            Image returnImage = Image.FromStream(ms);
            return returnImage;
        }

        private Bitmap ResizeImg(Image srcImage, int newWidth, int newHeight)
        {
            Bitmap newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format16bppRgb555);
            using (Graphics gr = Graphics.FromImage(newImage))
            {
                gr.SmoothingMode = SmoothingMode.HighQuality;
                gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gr.DrawImage(srcImage, new Rectangle(0, 0, newWidth, newHeight));
            }
            return newImage;
        }

        private bool CreateFTPDirectory(string directory, string ftpUser, string ftpPassword)
        {

            try
            {
                //create the directory
                FtpWebRequest requestDir = (FtpWebRequest)FtpWebRequest.Create(new Uri(directory));
                requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
                requestDir.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                requestDir.UsePassive = true;
                requestDir.UseBinary = true;
                requestDir.KeepAlive = false;
                FtpWebResponse response = (FtpWebResponse)requestDir.GetResponse();
                Stream ftpStream = response.GetResponseStream();

                ftpStream.Close();
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

        private bool FtpFileExists(string directoryPath, string ftpUser, string ftpPassword)
        {
            bool IsExists = true;
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(directoryPath);
                request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                request.Method = WebRequestMethods.Ftp.GetFileSize;

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                response.Close();
            }
            catch (WebException ex)
            {
                IsExists = false;
            }
            return IsExists;
        }

        public int GetOriginalLengthInBytes(string base64string)
        {
            if (string.IsNullOrEmpty(base64string)) { return 0; }

            var characterCount = base64string.Length;
            var paddingCount = base64string.Substring(characterCount - 2, 2)
                                           .Count(c => c == '=');
            return (3 * (characterCount / 4)) - paddingCount;
        }
    }
}