# Influx DB

## Prerequisites
- Visual Studio (any version that supports .Net Standard 2.1)
- [InfluxDB.Client NuGet package](https://www.nuget.org/packages/InfluxDB.Client/)
- [XMPro IoT Framework NuGet package](https://www.nuget.org/packages/XMPro.IOT.Framework/3.0.2-beta)
- Please see the [Building an Agent for XMPro IoT](https://docs.xmpro.com/lessons/writing-an-agent-for-xmpro-iot/) guide for a better understanding of how the XMPro IoT Framework works.

## Agent Descriptions
* [Action Agent](docs/actionagent.md)
The *Influx DB Action Agent* allows a specified Influx database and bucket to be updated with measurements from stream data at any point in the flow. In other words, this agent receives data and writes it as measurements to an Influx database bucket.

* [Listener](docs/listener.md)
The *Influx Db Listener* monitors the new records allowing the user to see a list with new records from last inserted record reported to the moment when the listener agent was started

* [Context Provider](docs/contextprovider.md)