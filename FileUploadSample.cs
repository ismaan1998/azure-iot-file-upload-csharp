// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AZ220Course.IoTHub.UploadFile
{
    public class FileUploadSample
    {
        private readonly DeviceClient _deviceClient;

        public FileUploadSample(DeviceClient deviceClient)
        {
            _deviceClient = deviceClient;
        }

        public async Task RunSampleAsync()
        {
            const string filePath = "scenery.jpg";

            using var fileStreamSource = new FileStream(filePath, FileMode.Open);
            var fileName = Path.GetFileName(fileStreamSource.Name);

            Console.WriteLine($"Uploading file {fileName}");

            var fileUploadSasUriRequest = new FileUploadSasUriRequest
            {
                BlobName = fileName
            };

            // 1. Get the connection details to the Storage Account from the IoT Hub
            Console.WriteLine("Getting SAS URI from IoT Hub to use when uploading the file...");
            FileUploadSasUriResponse sasUri = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest);
            Uri uploadUri = sasUri.GetBlobUri();

            Console.WriteLine($"Successfully got SAS URI ({uploadUri}) from IoT Hub");

            try
            {
                Console.WriteLine($"Uploading file {fileName} using the Azure Storage SDK and the retrieved SAS URI for authentication");

                // 2. Upload the file to the Storage Account
                var blockBlobClient = new BlockBlobClient(uploadUri);
                await blockBlobClient.UploadAsync(fileStreamSource, new BlobUploadOptions());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload file to Azure Storage using the Azure Storage SDK due to {ex}");

                var failedFileUploadCompletionNotification = new FileUploadCompletionNotification
                {
                    CorrelationId = sasUri.CorrelationId,
                    IsSuccess = false,
                    StatusCode = 500,
                    StatusDescription = ex.Message
                };

                await _deviceClient.CompleteFileUploadAsync(failedFileUploadCompletionNotification);
                Console.WriteLine("Sent failure notification to IoT Hub");

                return;
            }

            Console.WriteLine("Successfully uploaded the file to Azure Storage");

            // 3. Notify the IoT Hub on the success of the upload
            var successfulFileUploadCompletionNotification = new FileUploadCompletionNotification
            {
                CorrelationId = sasUri.CorrelationId,
                IsSuccess = true,
                StatusCode = 200,
                StatusDescription = "Success"
            };

            await _deviceClient.CompleteFileUploadAsync(successfulFileUploadCompletionNotification);
            Console.WriteLine("Notified IoT Hub that the file upload succeeded and that the SAS URI can be freed.");

            
        }
    }
}