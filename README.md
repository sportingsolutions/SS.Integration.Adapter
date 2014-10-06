SS.Integration.Adapter
======================

This is the master repository for the Sporting Solutions Adapter Client for integrating Sporting Solutions Connect with customers' trading platform.
The Adapter's basic functionality allows the receiving of Connect data (JSON format) and transforms it into user-friendly .NET objects.

Data is streamed from Connect directly into the adapter.

Usage of this Adapter requires a Connect username and password (available on request), it's usage is authorized only for current or prospective clients.

Any bug reports, comments, feedback or enhancements requests are gratefully received.

Dependencies
----------------------
You will need [Microsoft .NET Framework 4.5](http://www.microsoft.com/en-gb/download/details.aspx?id=30653) to compile and use the client on Windows

Additional dependencies can be obtained using NuGet (http://docs.nuget.org/docs/start-here/installing-nuget) - the solution is configured to obtain the correct packages.

Licence
----------------------
Sporting Solutions Adapter Client for the .Net Framework is licensed under the terms of the Apache Licence Version 2.0, please see the included Licence.txt file.

Getting Started
----------------------

### Plugins

The adapter's main functionality is to retrieve data from Connect. As data manipulation and data integration are platform specific, the adapter can be extended using plug-ins.

When loaded, the adapter searches for a plug-in in its installation directory. If no plug-in is found, the adapter stops its execution with an error message.

A plug-in is a custom object that implements the interface SS.Integration.Adapter.Model.Interface.IAdapterPlugin and it is encapsulated within its own dll.

The adapter communicates with the plug-in using this interface. It is down to the plug-in to manipulate the data and integrate it with the targeted trading platform.
 
There must be only one plug-in in the installation directory. If more than one plug-in is present, the adapter behaviour is not defined.

###Default Plugin

The adapter provides a default plug-in that prints a log message each time the adapter sends a command to it.

### Creating a plug-in

In order to create a new plug-in, several steps are necessary. In general, these are the common steps to be followed:

1. Create a new .NET class library project
2. Add the adapter dll (or .NET project) as a dependency of the newly created project
3. Create a new class that implements IAdapterPlugin interface
4. Fill the IAdapterPlugin's methods with your specific business-logic.
5. Copy the plugin binary and its dependencies into the adapters installation directory

### Plug-in configuration

#### Logging

The adapter provides logging functionalities through the log4net framework (version 1.2.10, http://www.nuget.org/packages/log4net/1.2.10 ). However, it is a plug-in developer's job to define the log4net configuration.

A basic log4net.config file could be:

```
<log4net>
  <appender name="FA" type="log4net.Appender.RollingFileAppender">
    <file value="C:\A_Path" />
    <appendToFile value="true" />
    <rollingStyle value="Size" />
    <maxSizeRollBackups value="100" />
    <maximumFileSize value="10MB" />
    <staticLogFileName value="true" />
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%utcdate [%thread] %-5level %logger - %message%newline" />
    </layout>
  </appender>

  <appender name="CA" type="log4net.Appender.ConsoleAppender">
    <layout type="log4net.Layout.PatternLayout">
      <conversionPattern value="%utcdate [%thread] %-5level %logger - %message%newline" />
    </layout>
  </appender>

  <logger name="SS" >
    <level value="DEBUG" />
    <appender-ref ref="FA" />
    <appender-ref ref="CA" />
  </logger>

  <logger name="SportingSolutions">
    <level value="DEBUG" />
    <appender-ref ref="FA" />
  </logger>
</log4net>
```

#### Configuration

Most of the time plug-ins need specific configuration files. As the plug-in is not the application entry point, standard .NET App.config files are not so easy to read/obtain. For this reason, the adapter implements functionality that allows plug-ins to access specific App.config files in an easy way. In order to take advantage of this functionality, these steps have to be followed:

1. Define an App.config file within the plug-in project
2. Rename the App.config file in something more appropriate, for example, "MyCompanyPlugin.config"
3. Fill this file with all the necessary parameters
4. On the plug-in code, call:

```c#
SS.Integration.Adapter.Model.ModuleConfigurationProvider.GetModuleConfiguration("MyCompanyPlugin.config"). 
```

this will allow the plug-in to obtain a System.Configuration.Configuration object that represents the config file. 

Adapter Configuration Settings
----------------------

The adapter comes with a number of app settings found inside the adapters app.config. In the large majority of cases the default value will be fine, however in some cases you may wish to change some value.
The following is a list of available settings.
- User - The username to authenticate with the Connect platform
- Password - The password to authenticate with the Connect platform
- URL - The url for the api of the Connect platform
- NewFixtureCheckerFrequency - The interval in milliseconds between checks for new fixtures in the feed
- StartingRetryDelay - The adapter operates a retry fallback strategy on failed HTTP requests. This is the time in milliseconds it will wait before retrying the failed request for the first time
- MaxRetryDelay - The adapter operates a retry fallback strategy on failed HTTP requests. This is the maximum amount of time in milliseconds it will wait after multiple failed attempts
- MaxRetryAttempts - The adapter operates a retry fallback strategy on failed HTTP requests. This is the maximum number of attempts before throwing an exception
- EchoInterval - The interval in milliseconds between the sending of echo messages to the connect platform
- EchoDelay - The maximum amount of time in milliseconds that will be allowed for an echo message to arrive
- FixtureCreationConcurrency - The maximum number of concurrent threads used to create fixtures
- SuspendAllOnShutdown - Suspend all fixtures when the Adapter is shutdown correctly e.g. Stop as a windows service
- EventStateFilePath - The path and filename of the eventstate file. This is used to store fixture sequence numbers so that the adapter can work out if it has missed updates.
- MarketFilterState - The path to the folder that holds the MarketFilterState. This is where the current state of each market is held.
- CacheExpiryInMins - The number of minutes that a markets state will be held in memory after being read. The timer is set back to this value and restarts the coutdown on each read.
- StatsEnabled - This should be set to false. It may be used in future for statistics generation.
- DeltaRuleEnabled - Set to true to turn on the delta rule. This will remove any markets and selections from a snapshot that have not changed since the last successfully processed sequence number

Adapter Market Rule
----------------------

The adapter contains a basic rules engine that allows markets to be edited or removed before being sent onto any plugin. One optional rule will filter out any market that is pending and has never been active.
This effectively means that a market will not appear to the plugin until it becomes active for the first time. To turn this rule on for Football and not apply it to match winner markets you will need to add the following code to your plugins initialise method.

```c#
var marketRuleList = new List<IMarketRule>();
var pendingMarketFilteringRule = new PendingMarketFilteringRule();
pendingMarketFilteringRule.AddSportToRule(SS.Integration.Adapter.Model.Football);
pendingMarketFilteringRule.ExcludeMarketType("match_winner");
MarketRules = marketRuleList;
```

Modules
----------------------

The adapter solution comes with these packages:

- SS.Integration.Adapter.WindowService - containing the adapter's entry point as a windows service
- SS.Integration.Adapter.Adapter - adapter core package
- SS.Integration.Adapter.Model
- SS.Integration.Adapter.MarketRules  - Default Rules used to filter markets from the feed
- SS.Integration.Adapter.Plugin.Model - these two packages define data structures that can be used by plug-ins
- SS.Integration.Common - utility and extra common functionalities
- SS.Integration.Adapter.Plugin.Logger - Default plug-in
- SS.Integration.Adapter.Tests - Adapter's test package 



