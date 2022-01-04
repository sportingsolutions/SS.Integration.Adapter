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

When loaded, the adapter searches for a plug-in in its installation directory with assembly name from Adapter config parameter "pluginAssembly". If no plug-in is found, the adapter stops its execution with an error message.

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
- StateProviderPath - The path relative to Adapter Directory where all the state files are saved.
- FixturesStateFilePath - The filename or file path relative to StateProviderPath or full rooted file path of the fixtures state file. This is used to store fixture sequence numbers so that the adapter can work out if it has missed updates.
- FixturesStateAutoStoreInterval - The interval in milliseconds at which the fixtures state file is saved to disk from internal adapter's memory.
- marketFiltersDirectory - The path relative to StateProviderPath for the directory that holds the MarketFilterState. This is where the current state of each market is held.
- CacheExpiryInMins - The number of minutes that a markets state will be held in memory after being read. The timer is set back to this value and restarts the coutdown on each read.
- StatsEnabled - This should be set to false. It may be used in future for statistics generation.
- DeltaRuleEnabled - Set to true to turn on the delta rule. This will remove any markets and selections from a snapshot that have not changed since the last successfully processed sequence number
- MaxUnhandledExceptions - The number of fatal exception that adapter should bypass and try to restart.
- UseSupervisor - true/false to enable/disable Supervisor functionality.
- SupervisorUrl - Url of the Supervisor UI.
- SkipRulesOnError - Setting to skip processing market rules when processing snapshot errored.
- StreamSafetyThreshold - Number of sequences that adapter is allowed to skip (miss processing) on stream validation
- SkipRestartOnFatalException - Setting to skip restarting adapter when fatal errors occur. This setting invalidates MaxUnhandledExceptions setting.
- PreMatchSuspensionBeforeStartTimeInMins - Number of minutes passed from match start time allowed to avoid suspension on stream disconnection.
- DisablePrematchSuspensionOnDisconnection - true/false to Disable/Enable fixture suspension on stream disconnection.
- StartStreamingTimeoutInSeconds - number of seconds to wait before logging warn message when trying to connect to the streaming server.
- AllowFixtureStreamingInSetupMode - true/false to Enable/Disable fixture streaming when is in Setup mode.
- PluginAssembly - the name of plugin assembly which has implementation of SS.Integration.Adapter.Model.Interfaces.IAdapterPlugin
- MaxFixtureUpdateDelayInSeconds - setting indicates an acceptable latency between feed stream update and current time - please do not set to 0 as it will cause validdation to fail on every update. 
- DelayedFixtureRecoveryAttemptSchedule - when the streaming starts Adapter runs a verification whether the snapshot is needed, the check requires a call to API which doesn't need to be repeated for the set delay in seconds. Setting it very low (below 10) can result in start streaming delays. 
- AutoReconnect - this refers to RabbitMQ client autoreconnect capability - recommended setting is true for Adapters streaming below 500 fixtures and false on any value above 


Adapter Market Rules
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

There are 3 default Market Rules implemented:

- InactiveMarketsFilteringRule

This rule removes markets from snapshots/updates if:

  1) The market is INACTIVE
  2) The market was INACTIVE in the previous update/snapshot

If there is a change in market's name or status the market will not be removed.

- PendingMarketFilteringRule

This rule removes from a snapshot/update all the markets that are not (and never were) active.
Once a market becomes active for the first time, if the update is not a snapshot, the rules will add all the missing information to the market/selections (i.e. tags).
It is possible to configure the rule by sport and allow specific markets to be excluded from this rule.

- DeletedMarketsRule

This rule suspends the market if the fixture is deleted from the API.

- VoidUnSettledMarket

This rule is executed only if the match is over.
Any call to this rule while the match is not over, will result in an empty set of intents.
The purpose of this rule is to void any market that:

  1) Has been active at least once 
  2) Has been seen/processed by the plugin (or in other words, during the market life cycle, at least once it appeared on a snapshot/update passed down to the plugin)
  3) Has not been settled or voided by the platform.

The main use of this rule is during market re-definition.
If a market definition changes when the fixture is already published, some of the markets could disappear from the snapshots/updates, because it wasn't included in the new definition.
At the end of the match, this rule will take care of voiding these markets.


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


Supervisor UI
----------------------
Important: The UI has been only tested with Chrome and may not be compatible with other browsers. 

Supervisor UI is a management UI for Adapter streaming component. It allows you to view the sports and fixtures Adapter is processing (streaming/actively checking). It does not give you access to fixtures which are ignored by Adapter like already processed Match Over fixtures. 

In order to enable supervisor you need to update the setting 'useSupervisor' = true in the Adapter config file. 
Once supervisor is enabled you will have access to it's management UI at local address http://localhost:9000/ui

#### Main Supervisor UI
![Main UI](/img/MainUI-SportsView.jpg)

The UI gives you access to all sports (where at least one fixture is available), please note that the count in the Total refers to fixtures which are in Setup/Prematch/In-play but excludes Over. For e.g. if you publish 10 fixtures in Setup and 10 in match over state the Total will show 10/0. The second number reflects the in-play fixtures. If the error count is above 0 it shows how many fixtures are affected with errors. 

### Fixtures list:
![FixturesView](/img/FixturesView.jpg)

In fixtures list you can preview all available fixtures with the basic data about them. 
You can also narrow done the list by using the filters on the top of the list:

![Filters](/img/FilterView.jpg)

You can either type fixture id/name or use status buttons to display only 'Prematch' or only in-play. 

### Fixture details view:

This is the most detailed view which allows you to see the individual fixture status. It shows updates processed as well as allowing you to force the Adapter to execute several actions(more on actions below). The UI is dynamic and there's usually no need to manually refresh it. The details showed on the UI may appear with a delay and that's not indicative on the Adapter performance. 

The overall view should look like this:
![FixtureDetailsView](/img/FixtureView.jpg)

At the top you can see a summary showing that the fixture is currently 'Connected' to the stream, together with the details of the currently processed sequence and epoch.
Please note that this will not be the case for fixtures in 'Setup', that's due to performance optimisation. Once the fixture reaches prematch it should connect automatically. 

When fixture is disconnected it may show similar details to this:

![FixtureDisconnected](/img/PrematchDisconnected.JPG)

Unless this persists for over 5mins please allow Adapter to recover. If the issue persists over 5 minutes and fixture is in prematch/in-play you should investigate a potential network issue which prevents Adapter from reconnecting. 

Stream updates panel

At the lower part of the screen you can see the list of sequences processed and their result. Please note that the most important row is the top which reflects the current state. If there was an error in sequence 5 but now you can see that sequence 12 is successfully processed it means Adapter has already recovered. 

##### Actions

Actions panel is located on the righthand side of the Fixture details view.  There are 3 actions available:
 - Restart - Stops the fixture and allows Adapter to start it again (it has very similar effect to the Delete/Republish that    you can do on the Connect Fixture Factory)
 - Snapshot - takes snapshot and sends to plugin. Please note this forces a full snapshot without any rules or filters                     applied
 - Clear State - deletes all state Adapter's state, please note that after this is done you're likely to receive a lot                     more updates as Adapter is no longer aware of what was previously processed.

The actions sholud only be invoked if you are trying to correct an issue with the fixture, there's no need to force any actions on the Adapter during the normal operation. 

#### Errors

Fixtures which are affected by errors are showed with a light-red background in the fixtures list:
![FixtureListWithErrors](/img/FixtureViewWithErrors.jpg)

The errors indicate that it was not possible to process the last update successfully, this can be any error raised by plugin or internal Adapter error. 

![Error](/img/Error.jpg)

Errors also show on the fixture details view:
![FixturePrematchErrored](/img/FixtureViewWithErrors.jpg)

Adapter's normal procedure when any error occurs is try again with a snapshot. However, if a fixture remains in an error state for a long period of time (over 5mins) you should check Adapter/plugin logs and investigate the cause. 






