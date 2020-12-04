# Action Agent
## How the code works
All settings referred to in the code need to correspond with the settings defined in the template that has been created for the agent using the Stream Integration Manager. Refer to the [Stream Integration Manager](https://docs.xmpro.com/courses/packaging-an-agent-using-stream-integration-manager/) guide for instructions on how to define the settings in the template and package the agent after building the code. 

After packaging the agent, you can upload it to XMPro IoT and start using it.

### Settings

When a user needs to use the *Influx DB Listener* agent, they need to provide the name URL of the Influx DB Server they want to connect to, along with a Token and the Org that can be used for connection. Optionally, a user will be able to specify these values via variables which are indicated in the config with a 'v' in front of the name. Retrieve these values from the configuration using the following code: 

```csharp
private Configuration config;
private string URL => this.config["URL"];
private string Token => this.config["Token"];
private string Org => this.config["Org"];
```
Then user will have to provide the Bucket name for writing the data.

```csharp
private string Bucket => this.config["Bucket"];
```
### Configurations
In the *GetConfigurationTemplate* method, parse the JSON representation of the settings into the Settings object.
```csharp
var settings = Settings.Parse(template);
new Populator(parameters).Populate(settings);
```
Next, retrieve the corresponding control for each setting, including potential variables, and get its value according to whether the UseVariables checkbox is set:
```csharp
CheckBox useVariables = settings.Find(UseVariablesConst) as CheckBox;
TextBox URLTextBox = settings.Find(URLConst) as TextBox;
TextBox tokenTextBox = settings.Find(TokenConst) as TextBox;
TextBox orgTextBox = settings.Find(OrgConst) as TextBox;
VariableBox URLVarBox = settings.Find("v" + URLConst) as VariableBox;
VariableBox tokenVarBox = settings.Find("v" + TokenConst) as VariableBox;
VariableBox orgVarBox = settings.Find("v" + OrgConst) as VariableBox;


URLTextBox.Required = tokenTextBox.Required = orgTextBox.Required = URLTextBox.Visible = tokenTextBox.Visible = orgTextBox.Visible = useVariables.Value != true;
URLVarBox.Required = tokenVarBox.Required = orgVarBox.Required = URLVarBox.Visible = tokenVarBox.Visible = orgVarBox.Visible = useVariables.Value == true;

string url;
string token;
string org;

if (useVariables.Value)
{
    url = GetVariableValue(orgVarBox.Value);
    token = GetVariableValue(tokenVarBox.Value);
    org = GetVariableValue(orgVarBox.Value);
}
else
{
    url = URLTextBox.Value;
    token = tokenTextBox.Value;
    org = orgTextBox.Value;
}

```
All Org buckets needs to be listed in a drop-down in order to allow user to select the bucket in which the agent searches for new records. Additionally, Multiple tags and fields are able to be placed on one data point so the ability to specify which columns represent tags and fields respectively so the user must be able to select those values via a dropdown of incoming columns.
```csharp
if (InfluxDbService == null &&
    URLTextBox != null && !string.IsNullOrWhiteSpace(URLTextBox.Value) &&
    tokenTextBox != null && !string.IsNullOrWhiteSpace(tokenTextBox.Value) &&
    orgTextBox != null && !string.IsNullOrWhiteSpace(orgTextBox.Value))
{
    InfluxDbService = new InfluxDbService(URLTextBox.Value, tokenTextBox.Value, orgTextBox.Value);
}
if (InfluxDbService != null)
{
    DropDown bucketsDropDown = settings.Find(BucketConst) as DropDown;
    List<Bucket> bucketsList = this.InfluxDbService.GetBucketsList().Result;
    if (bucketsDropDown != null && bucketsList != null)
    {
        bucketsDropDown.Options = bucketsList.Select((bucket => new Option() { DisplayMemeber = bucket.Name, ValueMemeber = bucket.Name })).ToList();
    }
    if (!string.IsNullOrEmpty(bucketsDropDown.Value))
    {
        InfluxDbService.Bucket = bucketsDropDown.Value;
    }
 }
```
### Validate
For this agent to be successfully added, the following needs to be true:
* The URL of the Influx DB Server should have a value.
* The Token should have an entered value.
* The Org should have an entered value.
* The Bucket should have selected a value.
```csharp
int i = 1;
List<string> errors = new List<string>();
this.config = new Configuration() { Parameters = parameters };

if (string.IsNullOrWhiteSpace(this.URL))
    errors.Add($"Error {i++}: Url is not specified.");
if (string.IsNullOrWhiteSpace(this.Token))
    errors.Add($"Error {i++}: Token is not specified.");
if (string.IsNullOrWhiteSpace(this.Org))
    errors.Add($"Error {i++}: Org is not specified.");
if (string.IsNullOrWhiteSpace(this.Bucket))
    errors.Add($"Error {i++}: Bucket is not specified.");

return errors.ToArray();
```
### Create
Set the config variable to the configuration received and create the InfluxDbService in the *Create* method.
```csharp
public void Create(Configuration configuration)
{
    this.config = configuration;
    if (InfluxDbService == null)
    {
        this.InfluxDbService = new InfluxDbService(URL, Token, Bucket, Org, ReadWriteTimeout, SocketTimeout, Convert.ToBoolean(EnableGZip));
    }
    this._inputParameters = new Dictionary<string, Types>(); //map the inputParameters to a dictionary to quickly retrieve them as needed at runtime.
    ParentOutputs.ForEach(i => _inputParameters.Add(i.Name, i.Type));
}
```

### Start
There is no need to do anything in the *Start* method.

### Destroy
There is no need to do anything in the *Destroy* method.

### Publishing Events
This agent requires you to implement the *IReceivingAgent* interface; thus, the *Receive* method needs to be added to the code. 

Each of the incoming items needs to be written to the influx database bucket with tags and field appropriately assigned to the datapoint. 
```csharp
public void Receive(string endpointName, JArray events, JArray mappedEvents)
{
    try
    {
        for (int i = 0; i < events.Count; i++)
        {
            JObject _event = events[i] as JObject;
            JObject mapped = mappedEvents[i] as JObject;
            string measurementTitle = mapped[MeasurementTitleInputConst].ToString();
            PointData data = PointData.Measurement(measurementTitle)
                                        .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

            foreach (var row in TagMapping.Rows)
            {
                string tagName = row[TagMappingColumnNameConst].ToString();
                string tagValue = _event[row[TagMappingColumnKeyConst].ToString()].ToString();
                data = data.Tag(tagName, tagValue);
            }
            foreach (var row in FieldMapping.Rows)
            {
                string fieldName = row[FieldMappingColumnNameConst].ToString();
                string fieldValue = _event[row[FieldMappingColumnKeyConst].ToString()].ToString();
                data = AddField(data, fieldName, fieldValue, _inputParameters[row[FieldMappingColumnKeyConst].ToString()]);
            }
            InfluxDbService.WritePoint(data);
        }

        this.OnPublish(this, new OnPublishArgs(events, OutputEndpoint));
    }
    catch(Exception ex)
    {
        this.OnPublishError?.Invoke(this, new OnErrorArgs(this.UniqueId, DateTime.UtcNow, nameof(Receive), ex.Message, ex.InnerException?.Message ?? ""));
    }
}
```

### OnVariableRequest Event
When a server variable is used within the agent, invoke the OnVariableRequest event using the *GetVariableValue* method to retrieve the variable's value.

```csharp
private string GetVariableValue(string variableName, bool isEncrypted = false)
{
    var x = new OnVariableRequestArgs(variableName);
    this.OnVariableRequest?.Invoke(this, x);
    return isEncrypted ? this.Decrypt(x.Value) : x.Value;
}
```

### OnDecryptRequest
When a control is secure, retrieving the actual value requires decrypting the value before use within the agent. To do this, invoke the OnDecryptRequest event using the *Decrypt* method to retreive the control's value.

```csharp
private string Decrypt(string value)
{
    var request = new OnDecryptRequestArgs(value);
    this.OnDecryptRequest?.Invoke(this, request);
    return request.DecryptedValue;
}
```

### Custom Influx DB Service Class
This agent uses a custom service class, implemented to facilitate the communication with InfluxDBClient.
In service contructor we create a *InfluxDBClient* object(used to acess the Influx DB) using the server URL, then we authenticate based on the provided token, seting the Organisation received as a parameter

```csharp
public InfluxDbService(string url, string token, string org)
{
    Client = InfluxDBClientFactory.Create(url, token.ToCharArray());
    Org = org;
}
```
Next sections will exemplify few interactions with the Influx DB Client implemnted into this custom service: </br>

Get all the *Buckets* from the authenticated organisation.

```csharp
public async Task<List<Bucket>> GetBucketsList()
{
    var bucketsApi = Client.GetBucketsApi();
    return await bucketsApi.FindBucketsByOrgNameAsync(Org);
}
```
Push a new record into the bucket.

```csharp
  public string WritePoint(PointData point)
        {
            string result = string.Empty;
            try
            {
                using (var writeApi = Client.GetWriteApi())
                {
                    writeApi.WritePoint(Bucket, Org, point);
                }
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }
```
