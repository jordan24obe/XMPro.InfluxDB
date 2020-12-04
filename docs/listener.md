# Influx DB Listener

## How the code works
All settings referred to in the code need to correspond with the settings defined in the template that has been created for the agent using the Stream Integration Manager. Refer to the [Stream Integration Manager](https://docs.xmpro.com/courses/packaging-an-agent-using-stream-integration-manager/) guide for instructions on how to define the settings in the template and package the agent after building the code. 

After packaging the agent, you can upload it to XMPro IoT and start using it.

### Settings
When a user needs to use the *Influx DB Listener* agent, they need to provide the name URL of the Influx DB Server they want to connect to, along with a Token and the Org that can be used for connection. Optionally, a user will be able to specify these values via variables which are indicated in the config with a 'v' in front of the name. Retrieve these values from the configuration using the following code: 

```csharp
private Configuration config;
private bool UseVariables => bool.TryParse(this.config[UseVariablesConst], out bool result) && result;
private string URL => UseVariables ? GetVariableValue(this.config["v" + URLConst]) : this.config[URLConst];
private string Token => UseVariables ? GetVariableValue(this.config["v" + TokenConst], true) : this.config[TokenConst];
private string Org => UseVariables ? GetVariableValue(this.config["v" + OrgConst]) : this.config[OrgConst];
```
Then user will have to provide the Bucket name, Measurement name, Tag Value and Field Key for quering the data in order to identify new records.

```csharp
private string Bucket => this.config["Bucket"];
private string MeasurementTitle => this.config["Measurement"];
private string MeasurementTagValue => this.config["TagValue"];
private string MeasurementFieldValue => this.config["FieldKey"];
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
if (!XMIoT.Framework.Helpers.StringExtentions.IsNullOrWhiteSpace(url, token, org))
{
    InfluxDbService = new InfluxDbService(url, Decrypt(token), orgTextBox.Value);
}
if (InfluxDbService != null)
{
    DropDown bucketsDropDown = settings.Find(BucketConst) as DropDown;
    List<Bucket> bucketsList = this.InfluxDbService.GetBucketsList().Result;
    if (bucketsDropDown!=null && bucketsList != null)
    {
        bucketsDropDown.Options = bucketsList.Select((bucket => new Option() { DisplayMemeber = bucket.Name, ValueMemeber = bucket.Name })).ToList();
    }
    if (!string.IsNullOrEmpty(bucketsDropDown.Value))
    {
        InfluxDbService.Bucket = bucketsDropDown.Value;
    }
    TextBox readTimeoutTextBox = settings.Find(ReadWriteTimeoutConst) as TextBox;
    TextBox socketTimeoutTextBox = settings.Find(SocketTimeoutConst) as TextBox;
    TextBox enableGzipTextBox = settings.Find(EnableGZipConst) as TextBox;

    Grid list = settings.Find(FieldMappingsConst) as Grid;
    var fieldsDropdown = list.Columns.Find(c => c.Key == FieldMappingColumnKeyConst) as DropDown;
    fieldsDropdown.Options = ParentOutputs.Select(o => new Option() { DisplayMemeber = o.Name, ValueMemeber = o.Name }).ToList();

    Grid tags = settings.Find(TagMappingsConst) as Grid;
    var tagsDropdown = tags.Columns.Find(c => c.Key == TagMappingColumnKeyConst) as DropDown;
    tagsDropdown.Options = ParentOutputs.Select(o => new Option() { DisplayMemeber = o.Name, ValueMemeber = o.Name }).ToList();

    if (enableGzipTextBox != null && !string.IsNullOrWhiteSpace(enableGzipTextBox.Value))
    {
        InfluxDbService.EnableGZip = Convert.ToBoolean(enableGzipTextBox);
    }
}
```

### Validate
For this agent to be successfully added, the following needs to be true:
* The URL of the Influx DB Server should have a value.
* The Token should have an entered value.
* The Org should have an entered value.
* The Bucket should have selected a value.
* The Measurement should have an entered value.
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
if (string.IsNullOrWhiteSpace(this.MeasurementTitle))
    errors.Add($"Error {i++}: Measurement is not specified.");
if (string.IsNullOrWhiteSpace(this.MeasurementTagValue))
    errors.Add($"Error {i++}: TagValue is not specified.");
if (string.IsNullOrWhiteSpace(this.MeasurementFieldValue))
    errors.Add($"Error {i++}: FieldKey is not specified.");

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
        this.InfluxDbService = new InfluxDbService(URL, Decrypt(Token), Bucket, Org);
    }
}
```
### Start
In the *Start* method, set the agent to query for new times after the time it was started.
```csharp
public void Start()
{
    _lastTimestamp = DateTime.UtcNow;
}
```

### Publishing Events
Publish the latest entries by implementing the query within the *Poll* method and invoking the *OnPublish* event.
```csharp
public void Poll()
{
    try
    {
        string query = CreateQuery(_lastTimestamp.AddSeconds(1).ToUniversalTime().ToString("s") + "Z");
        List<FluxTable> tables = InfluxDbService.GetQueryResult(query);
        IList<IDictionary<string, object>> results = new List<IDictionary<string, object>>();
        DateTime currentLastTimestamp = DateTime.MinValue;
        FluxTable fluxTable = tables[0];

        for (int i = 0; i < fluxTable.Records.Count; i++)
        {
            var record = fluxTable.Records[i];
            currentLastTimestamp = Convert.ToDateTime(record.GetValueByKey(TimeInfluxDbColumnNameConst).ToString());
            if (currentLastTimestamp > _lastTimestamp)
            {
                _lastTimestamp = currentLastTimestamp;
            }
            IDictionary<string, object> keyValuePairs = new Dictionary<string, object>();
            keyValuePairs.Add(TimeInfluxDbColumnNameConst, record.GetValueByKey(TimeInfluxDbColumnNameConst).ToString());
            foreach (var row in TagMapping.Rows)
            {
                string tagName = row[TagMappingColumnNameConst].ToString();
                string tagValue = row[TagMappingColumnValueConst].ToString();
                keyValuePairs.Add(tagName, tagValue);
            }
            keyValuePairs.Add(TagKeyName, record.GetValueByKey(TagKeyName));
            foreach (var table in tables)
            {
                var name = table.Records[i].GetValueByKey(FieldInfluxDbColumnNameConst).ToString();
                var value = table.Records[i].GetValueByKey(ValueInfluxDbColumnNameConst);
                keyValuePairs.Add(name, value);
            }
            keyValuePairs.Add(MeasurementsInfluxDbColumnNameConst, record.GetValueByKey(MeasurementsInfluxDbColumnNameConst));
            results.Add(keyValuePairs);
        }


        if (results.Count > 0)
            this.OnPublish?.Invoke(this, new OnPublishArgs(results.ToArray(), OutputConst));
    }
    catch(Exception ex)
    {
        this.OnPublishError?.Invoke(this, new OnErrorArgs(this.UniqueId, DateTime.UtcNow, nameof(Poll), ex.Message, ex.InnerException?.Message ?? ""));
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
Get query result.
```csharp
public List<FluxTable> GetQueryResult(string query)
{
    return Client.GetQueryApi().QueryAsync(query, Org).Result;
}
```
Where a query is formatted on the following idea.
```csharp
from(bucket: "selected's Bucket")
  |> range(start: lastTimeStamp)
  |> filter(fn: (r) => r["_measurement"] == "mem")
  |> filter(fn: (r) => r["host"] == "TagValue")
  |> filter(fn: (r) => r["_field"] == "TagKey")
```
