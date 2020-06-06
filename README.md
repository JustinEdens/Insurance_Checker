# Insurance_Checker
This is an AWS Lambda function that is triggered by S3, then parses the submitted XML file for patient ID, then puts that patient ID and 
a request ID in a Json object and sends that Json object to a input SQS. Then a worker Service pulls from the SQS using long pulling. 
Gets the patient ID, checks the insurance Database to see if they have insurance, then puts the patient ID, request ID, and Boolean 
Insurance in a Json object and sends it to a output SQS, where the Lambda function uses long pulling to receive messages from the output 
queue.

# Structure
* XML file gets submitted to S3
* Lambda gets triggered, parses XML
* creates Json object with patient ID and request ID
* sends Json object to input SQS queue
* Worker service pulls from input queue
* parses for patient ID
* checks Database for insurance
* creates Json object with patient ID and request ID, and insurance
* sends Json object to output SQS queue
* Lambda pulls from output queue
* Lambda prints whether or not they have insurance

# What I Learned
* How to create and deploy a service
* How to use long pulling to pull from AWS SQS queue
* How to create, access, and send messages to a AWS SQS queue
* How to deploy a AWS Lambda function
* How to add a trigger from AWS S3 to a AWS Lambda function
* How to create and configure a AWS S3 bucket
