﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatabaseManager.Server.Helpers;
using DatabaseManager.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.File;
using Microsoft.Extensions.Configuration;

namespace DatabaseManager.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly string connectionString;
        private readonly string container = "sources";
        private readonly IWebHostEnvironment _env;

        public FileController(IConfiguration configuration, IWebHostEnvironment env)
        {
            connectionString = configuration.GetConnectionString("AzureStorageConnection");
            _env = env;
        }

        [HttpGet("{datatype}")]
        public async Task<ActionResult<List<string>>> Get(string datatype)
        {
            List<string> files = new List<string>();
            try
            {
                CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
                CloudFileShare share = account.CreateCloudFileClient().GetShareReference(datatype);
                IEnumerable<IListFileItem> fileList = share.GetRootDirectoryReference().ListFilesAndDirectories();
                foreach (IListFileItem listItem in fileList)
                {
                    if (listItem.GetType() == typeof(CloudFile))
                    {
                        files.Add(listItem.Uri.Segments.Last());
                    }
                }
            }
            catch (Exception)
            {
                return NotFound();
            }

            return files;
        }

        [HttpPost]
        public async Task<ActionResult<string>> SaveData(FileParameters fileParams)
        {
            if (fileParams == null) return BadRequest();
            try
            {
                ConnectParameters connector = Common.GetConnectParameters(connectionString, container, fileParams.DataConnector);
                CloudStorageAccount account = CloudStorageAccount.Parse(connectionString);
                CloudFileClient fileClient = account.CreateCloudFileClient();
                CloudFileShare share = fileClient.GetShareReference(fileParams.FileShare);
                if (share.Exists())
                {
                    CloudFileDirectory rootDir = share.GetRootDirectoryReference();
                    CloudFile file = rootDir.GetFileReference(fileParams.FileName);
                    if (file.Exists())
                    {
                        string fileText = file.DownloadTextAsync().Result;
                        if (fileParams.FileShare == "logs")
                        {
                            LASLoader ls = new LASLoader(_env);
                            ls.LoadLASFile(connector, fileText);
                        }
                        else
                        {
                            string connectionString = connector.ConnectionString;
                            string[] fileNameArray = fileParams.FileName.Split('.');
                            CSVLoader cl = new CSVLoader(_env);
                            cl.LoadCSVFile(connectionString, fileText, fileNameArray[0]);
                        }
                    }
                    else
                    {
                        return BadRequest();
                    }
                }
                else
                {
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.ToString());
            }

            return Ok($"OK");
        }
    }
}