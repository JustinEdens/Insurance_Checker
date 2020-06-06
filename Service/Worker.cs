using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Xml;
using System.Xml.XPath;

namespace Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            String InputQueueURL = "https://sqs.us-east-1.amazonaws.com/799289016492/inputQueue";
            AmazonSQSClient client = new AmazonSQSClient();

            while (!stoppingToken.IsCancellationRequested)
            {
                //recive messages
                ReceiveMessageRequest request = new ReceiveMessageRequest()
                {
                    QueueUrl = InputQueueURL,
                    WaitTimeSeconds = 20
                }; 
                var response = client.ReceiveMessageAsync(request);

                //processes messages
                foreach(Message message in response.Result.Messages)
                {
                    Console.WriteLine("Read message. Message body is: {0}", message.Body);

                    //converts string into JsonPatient object
                    JsonPatient jp = JsonConvert.DeserializeObject<JsonPatient>(message.Body);

                    //sends message to output queue
                    SendMessage(jp);

                    //deletes message from queue
                    DeleteMessageRequest delete = new DeleteMessageRequest()
                    {
                        QueueUrl = InputQueueURL,
                        ReceiptHandle = message.ReceiptHandle
                    };
                    client.DeleteMessageAsync(delete);
                }

                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }

        private void SendMessage(JsonPatient jp)
        {
            String OutputQueueURL = "	https://sqs.us-east-1.amazonaws.com/799289016492/outputQueue";

            AmazonSQSClient amazonSQSClient = new AmazonSQSClient();

            //checks with database
            jp.Insurance = CheckInsurance(jp.PatientID);

            //sends message to output queue
            string message = "{ \"RequestID\":\"" + jp.RequestID + "\", \"PatientID\":\"" + jp.PatientID + "\", \"Insurance\":\"" + jp.Insurance + "\"}";
            SendMessageRequest request = new SendMessageRequest
            {
                QueueUrl = OutputQueueURL,
                MessageBody = message
            };

            SendMessageResponse sendMessageResponse = amazonSQSClient.SendMessageAsync(request).Result;
            if (sendMessageResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
            {
                Console.WriteLine("Message successfully sent to queue {0}", OutputQueueURL);
            }
        }

        private bool CheckInsurance(string patientID)
        {
            //parse XML
            XmlDocument doc = new XmlDocument();
            doc.Load("C:\\Users\\justi\\Desktop\\InsuranceDatabase.xml");
            
            string val = doc.DocumentElement.SelectSingleNode("patient[@id='" + patientID + "']").Attributes["hasInsurance"].Value;
            Console.WriteLine("db: {0}", val);
            if (val.Equals("yes"))
            {
                return true;
            }
            return false;
        }

    }
}
