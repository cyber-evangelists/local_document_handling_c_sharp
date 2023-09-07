using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocService
{
    public static class BaseURL
    {
        private static string baseUrl = "http://127.0.0.01:5000"; 

        public static string UploadFile = baseUrl + "/upload_file";

        public static string Get_File = baseUrl + "/get_file";
    }
}
