using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Util;
using System.Net;
using System.Xml;
using System.Xml.XPath;
using Newtonsoft.Json;
using System.Collections;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace InsuranceFunction
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }
        
        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var s3Event = evnt.Records?[0].S3;
            if(s3Event == null)
            {
                return null;
            }

            try
            {

                //array for request ID's
                ArrayList requests = new ArrayList();

                var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);

                //proccess input
                string responseBody = "";
                string patientID = "";

                Amazon.S3.Model.GetObjectRequest s3Request = new Amazon.S3.Model.GetObjectRequest
                {
                    BucketName = s3Event.Bucket.Name,
                    Key = s3Event.Object.Key
                };
                using (Amazon.S3.Model.GetObjectResponse respond = await S3Client.GetObjectAsync(s3Request))
                using (System.IO.Stream responseStream = respond.ResponseStream)
                using (System.IO.StreamReader reader = new System.IO.StreamReader(responseStream))
                {
                    try
                    {
                        
                        responseBody = reader.ReadToEnd();
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(responseBody);

                        //parse XML
                        //evaluates Xpath expressions
                        XPathNavigator nav;
                        //holds xml document
                        XPathDocument docNav;
                        //iterates through selected nodes
                        XPathNodeIterator nodeIter;
                        String idExpression = "/patient/id";
                        //opens xml
                        docNav = new XPathDocument(new XmlNodeReader(doc));
                        //create a navigator to query with Xpath
                        nav = docNav.CreateNavigator();

                        //gets id
                        nodeIter = nav.Select(idExpression);
                        nodeIter.MoveNext();
                        patientID = nodeIter.Current.Value;


                    }
                    catch (XmlException e)
                    {
                        //file is not xml
                        Console.WriteLine("Error: {0}", e.Message);
                    }
                }

                //send message to input queue
                String InputQueueURL = "https://sqs.us-east-1.amazonaws.com/799289016492/inputQueue";
                AmazonSQSClient amazonSQSClient = new AmazonSQSClient();

                //creating message
                //create requestID
                string requestID = Guid.NewGuid().ToString();
                //Adds id to request id array
                requests.Add(requestID);
                string message = "{ \"RequestID\":\"" + requestID + "\", \"PatientID\":\"" + patientID + "\"}";
                SendMessageRequest request = new SendMessageRequest
                {
                    QueueUrl = InputQueueURL,
                    MessageBody = message
                };

                //sending message
                SendMessageResponse sendMessageResponse = amazonSQSClient.SendMessageAsync(request).Result;
                if (sendMessageResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
                {
                    Console.WriteLine("Message successfully sent to queue {0}", InputQueueURL);
                }

                //wait for message from output
                String OutputQueueURL = "	https://sqs.us-east-1.amazonaws.com/799289016492/outputQueue";

                //while the request id array is not empty
                while (requests.Count > 0)
                {
                    ReceiveMessageRequest outRequest = new ReceiveMessageRequest()
                    {
                        QueueUrl = OutputQueueURL,
                        WaitTimeSeconds = 20
                    };

                    var outResponse = amazonSQSClient.ReceiveMessageAsync(outRequest);
                    foreach (Message m in outResponse.Result.Messages)
                    {
                        //get message from output queue
                        //converts string into JsonPatient object
                        JsonPatient jp = JsonConvert.DeserializeObject<JsonPatient>(m.Body);
                        //removes requestID from array
                        requests.Remove(jp.RequestID);

                        //handles response
                        if (jp.Insurance)
                        {
                            Console.WriteLine("Patient with ID {0} has medical insurance", jp.PatientID);
                        }
                        else
                        {
                            Console.WriteLine("Patient with ID {0}  does not have medical insurance", jp.PatientID);
                        }

                        //delete message from queue
                        DeleteMessageRequest delete = new DeleteMessageRequest()
                        {
                            QueueUrl = OutputQueueURL,
                            ReceiptHandle = m.ReceiptHandle
                        };
                        amazonSQSClient.DeleteMessageAsync(delete);
                    }
                }

                return response.Headers.ContentType;
            }
            catch(Exception e)
            {
                context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogLine(e.Message);
                context.Logger.LogLine(e.StackTrace);
                throw;
            }
        }
    }
}
