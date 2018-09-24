# AzureB2C_CustomEmailVerification
## General Design
**Prerequisites: Custom Javascript and Custom Policy**

To use custom email provider, you’ll have to set up your own email verification service. You can use Azure function as a start, use a cosmosDB or azure table as a storage for the passcode, and any third-party email service to send emails.

At the client-side, you can use custom Javascript to control the whole email verification experience:
1. After the user has provided an email address, the client needs to call the verification service to send out the code via email.
2. After the user has provided the code, the client needs to call the service to verify the code. Since Azure AD B2C needs to know if this email has been verified, the service also needs to return a verification token so later, the token can be verified with the your service from B2C.

At the B2C side, when persisting the email address to the directory, verify the address with your service by validating the verification token.

### User Flow Chart
![alt text](/imgs/data_flow.png)

### UI Design
1.	On self-asserted page, a custom Javascript needs to be used to control email verification experience.
2.	There should be a verification token claim in the self asserted technical profile. The textbox for the claim should be hidden from the user. After verifying the code, this value should be filled.
3.	Use original B2C Javascript to submit the form. This will also submit the verification token in the hidden textbox.

### User Flow
1. Self-Asserted page
  1. Enter Email address and click "Send a code"
  2. Custom javascript sends ajax request to your email verification service
    1. Request
      1. Email address
      2. CorrelationId (you can get this from UserJourneyContextProvider)
    2. Response
      1. Verification token
  3. Javascript update the UI based on the ajax response
  4. Enter the code and click "Verify"
  5. Custom javascript sends ajax request to your email verification service
    1. Request
      1. Email address
      2. CorrelationId
      3. Verification token
      4. Code
    2. Response
      1. Verification token
  6. Use custom Javascript to fill the hidden textbox with verification token
  7. Click submit
2. Submit the form to Azure AD B2C
  1. Use validation technical profile to invoke a restful call to your verification service to verify the token
    1. Request
      1. CorrelationId
      2. Email address
      3. Verification token
  2. Create user in Azure active directory
3. Issue a token

## Step-by-step setup
### Setup SendGrid email account
Goto [SendGrid](https://www.sendgrid.com/) and create an account.
#### Create API Key
Save the key to a safer place, it will be used in the future.
![alt text](/imgs/sendgrid_create_api.png)
#### Create a dynamic email template
![alt text](/imgs/sendgrid_create_template.png)
#### Create a version for the template
![alt text](/imgs/sendgrid_create_template_version.png)
#### Set the subject and the name of the email
![alt text](/imgs/sendgrid_set_subject.png)
#### Set the dynamic content of the email
Note that the verification code is a dynamic data in the email and can be replaced by the arguments in the API request
![alt text](/imgs/sendgrid_set_content.png)
#### Test the dynamic content with test data
![alt text](/imgs/sendgrid_test.png)
#### Save the template
### Test SendGrid API
Here we test the SendGrid API using [Postman](https://www.getpostman.com/), you may use other tools such as Curl.
#### Create a collection in Postman
You can share the same API key in this collection
![alt text](/imgs/postman_create_collection.png)
#### Create a SendEmail request
![alt text](/imgs/postman_create_request.png)
#### Verify the email is received in your email box
![alt text](/imgs/gmail_verify.png)
### Setup Azure Table Storage
This will be used to store status information about how OTP verification. Save the access key in a safe place. It will be used in the future.
![alt text](/imgs/Create_azure_storage_table.png)
### Setup Azure Function
First we need to install the Azure Table storage client library in Azure function. Then we'll create three Azure functions triggered by HTTPs requests.
#### SendEmailVerification
Please refer to the code in [SendEmailVerification.csx](SendEmailVerification.csx). This will randomly generate a 6-digit code, send the email verification through SendGrid and save the entry in Azure Table storage. The type of the function is anonymous.
#### VerifyCode
Please refer to the code in [VerifyCode.csx](VerifyCode.csx). This will verify the code with the entry stored in the Azure Table storage and send back a verification token for future validation. The type of the function is anonymous.
#### ValidateEmailVerification
Please refer to the code in [ValidateEmailVerification.csx](ValidateEmailVerification.csx). This will be invoked by Azure AD B2C to validate the verified email using the verification token. We use anonymous authentication as an example and you may use other type for the function.
#### Enable CORS
You also need to enable CORS for those Azure functions. Please refer to this [document](https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings#cors) for how to setup CORS. As an example, you can allow all origins by adding "\*" to the list.
### Setup Custom content
Upload [custom_content.html](custom_content.html) to your blob storage and enable CORS and public access. See this [document](https://docs.microsoft.com/en-us/azure/active-directory-b2c/active-directory-b2c-reference-ui-customization) for more details.
### Setup Custom policies
Upload [TRUSTFRAMEWORKBASE.xml](TRUSTFRAMEWORKBASE.xml), [TRUSTFRAMEWORKEXTENSIONS.xml](TRUSTFRAMEWORKEXTENSIONS.xml), [CUSTOMEMAILSIGNUP.xml](CUSTOMEMAILSIGNUP.xml) to your B2C tenant. Note, some of the values in the policy file needs to be modified to your own values.
### End to End Test
Run the policy. If you've setup above correctly, you will see an example of email verification through SendGrid.
