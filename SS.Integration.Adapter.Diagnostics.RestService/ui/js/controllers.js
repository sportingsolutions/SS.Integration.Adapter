/**
 * @license  Copyright 2014 Spin Services Limited
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

'use strict';


(function () {

    var controllers = angular.module('adapterSupervisorControllers', []);

    // models used by the angularjs' controllers - most of them
    // expand the data received by the server by adding extra data
    // don't rename, otherwise extend() doesn't work properly
    var models = {

        AdapterDetail: function AdapterDetail(){
            this.AdapterVersion = "";
            this.UdapiSDKVersion = "";
            this.PluginName = "";
            this.PluginVersion = "";
            this.RunningThreads = "";
            this.MemoryUsage = "";
        },

        SportDetail: function SportDetail() {

            this.Name = "";
            this.CssClass = "";
            this.Url = "";
            this.Fixtures = {},
            this.InErrorState = 0,
            this.Total = 0;
            this.InPlay = 0;
            this.InPreMatch = 0;
            this.InSetup = 0;

            this.initGroups = function () {
                return {
                    InPlay: { InErrorState: 0, Items: new Array() },
                    InPreMatch: { InErrorState: 0, Items: new Array() },
                    InSetup: { InErrorState: 0, Items: new Array() }
                };
            };
            
            this.groupFixtures = function () {

                this.FixtureGroups = this.initGroups();

                var outer = this;
                $.each(this.Fixtures, function (index, value) {
                    var group = null;
                    switch (value.State) {
                        // 0: setup - 1: Ready
                        case 0: case 1: group = outer.FixtureGroups.InSetup; break;
                            // 2: pre-match
                        case 2: group = outer.FixtureGroups.InPreMatch; break;
                            // 3: in-play, 4: match over
                        case 3: case 4: group = outer.FixtureGroups.InPlay; break;
                        default: break;
                    }

                    if (group) {
                        group.Items.push(value);
                        if (value.IsInErrorState) group.InErrorState++;
                    }

                    if (value.IsInErrorState) outer.InErrorState++;
                });
            };

            this.FixtureGroups = this.initGroups();
        },

        FixtureDetail: function FixtureDetail() {

            this.Id = "";
            this.IsStreaming = false;
            this.State = 0;
            this.IsInErrorState = false;
            this.StartTime = null;
            this.Competition = "";
            this.CompetitionId = "";
            this.Description = "";
            this.IsOver = false;
            this.IsDeleted = false;
            this.ProcessingEntries = new Array();

            // these fields are computed
            this.LastSequence = "";
            this.LastEpoch = ""; 
            this.LastException = "";
            this.GroupedProcessingEntries = new Array();

            this.FillData = function () {

                this.GroupedProcessingEntries = new Array();

                this.ProcessingEntries.sort(function (a, b) {return new Date(a.Timestamp) - new Date(b.Timestamp);});

                if (this.ProcessingEntries.length > 0) {
                    var lastEntry = this.ProcessingEntries[this.ProcessingEntries.length - 1];
                    this.LastEpoch = lastEntry.Epoch;
                    this.LastException = lastEntry.Exception;
                    this.LastSequence = lastEntry.Sequence;
                }

                var last = "";
                var outer = this;
                // this groups processing entries using the sequence number (we can have multiple processing entries with the same sequence)
                $.each(this.ProcessingEntries, function (index, value) {
                    if (last != value.Sequence) {
                        outer.GroupedProcessingEntries.push({ sequence: parseInt(value.Sequence), items: new Array() });
                        last = value.Sequence;
                    }

                    var group = outer.GroupedProcessingEntries[outer.GroupedProcessingEntries.length - 1].items;

                    // in the situation where a sequence update (i.e. seq=4) requires a new snasphot,
                    // what we will receive are three updates: 1) entry with seq=4, State = 1 (aka Processing) and IsUpdate=true
                    // and then 2) update with seq=4, State = 1 and IsUpdate=false, 
                    // 3) update with seq=4, State=0 (Processed), IsUpdate=false
                    // in other words we will never receive the closing update for 1) - we swap the state value here

                    if (group.length > 0) {
                        var entryValue = group[group.length - 1];
                        if (entryValue.State == 1)
                            entryValue.State = 0;
                    }

                    group.unshift(value);
                });
            };
        },
    };

    // set of helper functions
    var fn = {
    
        GetSportsDetails : function(array, config)
        {
            if (!array) return new Array();
            if (!Array.isArray(array)) array = [array];
            var result = new Array();

            $.each(array, function(index, value) {
                var detail = new models.SportDetail();
                $.extend(detail, value);
                detail.groupFixtures();
                detail.CssClass = "ssln-" + detail.Name.toLowerCase();
                detail.Url = config.fn.getSportPath(config.uiRelations.SportDetail, detail.Name);
                result.push(detail);
            });

            return result;
        },

        /**
         * Allows to search within a list of fixtures for
         * a fixture that contains, in at list one of its
         * own properties, a value that contains the
         * substrign specified by "data"
         * @param {Array<FixtureDetail>} fixtures
         * @param {String} data
         * @return FixtureDetail
         */
        SearchFixtures: function (fixtures, data) {
            
            if (!Array.isArray(fixtures)) fixtures = [fixtures];

            var ret = new Array();
            $.each(fixtures, function (index, value) {
                var keys = Object.keys(value);
                for (var key in keys) {
                    if (value.hasOwnProperty(keys[key])) {
                        var tmp = value[keys[key]];
                        if ($.type(tmp) === 'string') {
                            if (tmp.indexOf(data) > -1)
                                ret.push(value);
                        }
                    }
                }
            });

            return ret;
        },
    };


    /**
     * AdapterCtrl controller: used to display details about the adapter,
     * and error notifications. It listens for push notifications regarding the
     * adapter's state and any possibile errors 
     * see:
     * 1) MyConfig.pushNotifications.events.AdapterUpdate
     * 2) MyConfig.pushNotifications.events.Errors
     */
    controllers.controller('AdapterCtrl', ['$scope', '$location', '$rootScope', 'Supervisor',
        function ($scope, $location, $rootScope, Supervisor) {

            // as the current layout (index.html) uses ngView and we add
            // this controller outside ngView's supervision, the controller's scope
            // is destroyed only when we leave the ngView's routing capabilities
            
            $scope.searching = {};
            $scope.searching.data = null;
            $scope.searching.noResults = false;
            $scope.details = new models.AdapterDetail();

            // 1) get the adapter's details
            var promise = Supervisor.getAdapterDetails();
            if (!promise) return;

            //...shows the "loading..." message
            $rootScope.$broadcast('my-loading-started');

            promise['finally'](function () { $rootScope.$broadcast('my-loading-complete'); });
            promise.then(function (data) { $scope.details = data; });

            // 2) subscribe to push notifications
            var config = Supervisor.getConfig();
            var streaming = Supervisor.getStreamingService();
            streaming.adapterSubscription().subscribe();

            $scope.$on(config.pushNotification.events.Errors, function (events, args) {
                if (!args) return;

                $rootScope.$broadcast('on-error-notification-received', args);
            });

            // update the adapter's details with new data
            $scope.$on(config.pushNotification.events.AdapterUpdate, function (events, args) {$.extend($scope.details, args); });


            // if the scope is destroyed, stop listening to notifications
            $scope.$on('destroy', function () { streaming.adapterSubscription().unsubscribe(); });


            // TODO: get rid of this code after dev phase is completed
            $scope.testNotification = function () {
                $rootScope.$broadcast('on-error-notification-received', {text: new Date().toString()});
            }

            // this allows us to clear all the currently displayed notifications
            $scope.clearNotifications = function () {
                $rootScope.$broadcast('on-error-notification-clear-all');
            }

            // function for redirecting to the fixture's page when a fixture's id is given
            this.search = function () {
                if ($scope.searching.data && 0 !== $scope.searching.data.length) {
                    var searchPromise = Supervisor.searchFixture($scope.searching.data);
                    $scope.searching.data = null;
                    $scope.searching.noResults = false;

                    if (!searchPromise) return;

                    searchPromise.then(function (data) {
                        if (data && data.Id) $location.path('/ui/fixture/' + data.Id);
                        else  $scope.searching.noResults = true;
                    });
                }
            }
        }]);

    controllers.controller('SportListCtrl', ['$scope', '$routeParams', '$rootScope', 'Supervisor',
        function ($scope, $routeParams, $rootScope, Supervisor) {

            $scope.sports = [];

            // 1) get the list of sports
            var promise = Supervisor.getListOfSports();
            if (!promise) return;

            // this allows us to display the "waiting layer"
            $rootScope.$broadcast('my-loading-started');
            promise['finally'](function () { $rootScope.$broadcast('my-loading-complete'); });

            // 2) subscribe to push notifications
            var config = Supervisor.getConfig();
            var streaming = Supervisor.getStreamingService();


            promise.then(function (data) {
                $scope.sports = fn.GetSportsDetails(data, Supervisor.getConfig());

                // subscribe to push-notifications
                $.each($scope.sports, function (index, value) {
                    var tmp = streaming.sportSubscription(value.Name);
                    if (tmp != null) tmp.subscribe();
                });
            });

            // 3) set up event handlers

            $scope.$on(config.pushNotification.events.SportUpdate, function (event, args) {

                // convert data using fn.GetSportsDetails (it always returns an array)
                if (!args) return;

                var sport = fn.GetSportsDetails(args, config);
                if(!Array.isArray(sport) || 0 == sport.length) return;

                sport = sport[0];
                var i = 0;

                // if we have received a notifications, it means that the sport
                // exists in $scope.sports - this means that to display a new
                // sport, a page refresh is necessary. However, 
                // this is not really an issue, as in the majority of the cases 
                // the list of sports is immediately known by the adapter
                for (; i < $scope.sports.length; i++) {
                    if ($scope.sports[i].Name === sport.Name) { $.extend($scope.sports[i], sport); break; }
                }
            });

            $scope.$on('destroy', function () {
                $.each($scope.sports, function (index, value) {
                    var tmp = streaming.sportSubscription(value.Name);
                    if (tmp != null) tmp.unsubscribe();
                });
            });

        }]);

    controllers.controller('SportDetailCtrl', ['$scope', '$routeParams', '$rootScope', '$location', 'Supervisor',
        function ($scope, $routeParams, $rootScope, $location, Supervisor) {

            // if sportCode is missing, redirect the user to the homepage
            if (!$routeParams.hasOwnProperty("sportCode")) $location.path(Supervisor.getConfig().uiRelations.Home);

            $scope.sport = new models.SportDetail();
            $scope.sport.Name = $routeParams.sportCode;
            $scope.tab = "all";
            $scope.search = null;
            $scope.lastUpdate = Date.now();
            $scope.sportCode = $routeParams.sportCode;

            /**
             * Allows us to switch to the tab
             * indicated by "tab".
             * @param {string} tab
             */
            $scope.changeTab = function (tab) { $scope.tab = tab; };

            /** 
             * Returns the list of fixtures to display
             * in the current active tab, eventually filtered
             * by the user through the page's "search" functionality
             */
            $scope.getFixturesForTab = function(){

                // make sure we have some valid values
                if(!$scope.tab) $scope.tab = 'all';
                if ($scope.search == "") $scope.search = null;

                var fixtures = null;
                switch ($scope.tab) {
                    case 'in-play': fixtures = $scope.sport.FixtureGroups.InPlay.Items; break;
                    case 'in-prematch': fixtures = $scope.sport.FixtureGroups.InPreMatch.Items; break;
                    case 'in-setup': fixtures = $scope.sport.FixtureGroups.InSetup.Items; break;
                    default: fixtures = $scope.sport.Fixtures; break;
                }

                // filter using user specified data
                if ($scope.search) fixtures = fn.SearchFixtures(fixtures, $scope.search);

                return fixtures;
            };

            /**
             * Opens the fixture page
             * @param {String} fixtureId
             */
            $scope.openFixtureDetails = function (fixtureId) {
                
                if (!fixtureId) return;

                var config = Supervisor.getConfig();
                var path = config.uiRelations.FixtureDetail;
                path = config.fn.getFixturePath(path, fixtureId);

                // don't use window.location directly
                // or the route dispatcher will not work
                $location.path(path);
            }

            $scope.update = function ()
            {
                var promise = Supervisor.getSportDetail($scope.sportCode);
                if (!promise) $location.path(Supervisor.getConfig().uiRelations.Home);

                $rootScope.$broadcast('my-loading-started');
                promise['finally'](function () { $rootScope.$broadcast('my-loading-complete'); });

                promise.then(function (data) {

                    // there will always be only one sport, but 
                    // getSportsDetails returns an array...
                    var tmp = fn.GetSportsDetails(data, Supervisor.getConfig());
                    if (tmp.length && tmp.length > 0) $scope.sport = tmp[0];
                    $scope.lastUpdate = Date.now();
                });
            }

            // 1) get the sport's details
            $scope.update();

            // 2) subscribe to push notifications
            var config = Supervisor.getConfig();
            var streaming = Supervisor.getStreamingService();
            var tmp = streaming.sportSubscription($scope.sport.Name);
            if (tmp) tmp.subscribe();

            // 3) set up broadcasting message handlers
            $scope.$on(config.pushNotification.events.SportUpdate, function (event, args) {
                if (!args) return;

                var sport = fn.GetSportsDetails(args, config);
                if (!Array.isArray(sport) || 0 == sport.length) return;

                // sport updates don't contain any fixture information
                // hence, if we don't store the Fixtures list before using extend
                // then that call will overwrite the fixtures list
                var fixtures = $scope.sport.Fixtures;
                $.extend($scope.sport, sport[0]);
                $scope.sport.Fixtures = fixtures;
                $scope.sport.groupFixtures();
            });

            $scope.$on('destroy', function () { if (tmp != null) tmp.unsubscribe(); });

        }]);

    controllers.controller('FixtureDetailCtrl', ['$scope', '$routeParams', '$rootScope', '$location', 'Supervisor',
        function ($scope, $routeParams, $rootScope, $location, Supervisor) {

            // check if we have the fixtureId
            if (!$routeParams.hasOwnProperty("fixtureId")) $location.path(Supervisor.getConfig().uiRelations.Home);

            $scope.fixture = new models.FixtureDetail();
            $scope.fixture.Id = $routeParams.fixtureId;
            $scope.commandResult = -1;
            $scope.commandInProgress = 0;
            var sequenceDetailsVisibility = new Array();  // stores information about the visibility of the details table

            var postCommandProcessing = function (commandPromise) {
                if (!commandPromise) return;

                $scope.commandInProgress = 1;
                commandPromise.then(function (result) {
                    if (result) {
                        $scope.commandResult = 0;
                    } else {
                        $scope.commandResult = 1;
                    }
                    $scope.commandInProgress = 0;
                });
            };

            /**
             * Returns true if the details for the sequence
             * passes as "sequence" are visible
             * @param {String} sequence
             */
            $scope.isDetailVisibile = function (sequence) {
                if (sequenceDetailsVisibility[sequence] === undefined) return false;
                return sequenceDetailsVisibility[sequence];
            };

            /**
             * Shows/hides sequence's details
             * @param {String} sequence
             * @param {Boolean} value
             */
            $scope.setDetailVisible = function (sequence, value) { sequenceDetailsVisibility[sequence] = value; };

            /**
             * Allows to send a command to the server to acquire a new snapshot
             * for the fixture
             */
            $scope.takeSnapshot = function () {

                if ($scope.commandInProgress) return;
                $scope.commandResult = -1;
                postCommandProcessing(Supervisor.takeFixtureSnapshot($scope.fixture.Id));
            }

            /**
             * Allows to send a command to the server to clear 
             * the fixture's state
             */
            $scope.clearState = function () {
                if ($scope.commandInProgress) return;
                $scope.commandResult = -1;
                postCommandProcessing(Supervisor.clearFixtureState($scope.fixture.Id));
            }

            /**
             * Allows to send a command to the server to 
             * restart the fixture's listner
             */
            $scope.restartListener = function () {
                if ($scope.commandInProgress) return;
                $scope.commandResult = -1;
                postCommandProcessing(Supervisor.restartFixtureListener($scope.fixture.Id));
            }

            var promise = Supervisor.getFixtureDetail($routeParams.fixtureId);

            $rootScope.$broadcast('my-loading-started');

            promise['finally'](function () { $rootScope.$broadcast('my-loading-complete'); });

            promise.then(function (data) {
                $.extend($scope.fixture, data);

                // fill up all the missing data
                $scope.fixture.FillData();
            });


            // subscribe to push notifications
            var config = Supervisor.getConfig();
            var streaming = Supervisor.getStreamingService();
            var tmp = streaming.fixtureSubscription($scope.fixture.Id);
            if (tmp) tmp.subscribe();

            $scope.$on(config.pushNotification.events.FixtureUpdate, function (event, args) {

                if (!args) return;

                // fixture updates might not contain new processing entries...
                var entries = $scope.fixture.ProcessingEntries;
                $.extend($scope.fixture, args);
                $scope.fixture.ProcessingEntries = entries;

                if (args.ProcessingEntries && args.ProcessingEntries.length > 0) {
                    // we can take advantage of the fact that the processing entries are sorted
                    // using the timestamp and the update might only contain new entries
                    // with greater timestampss
                    $.each(args.ProcessingEntries, function (index, value) {

                        var i = $scope.fixture.ProcessingEntries.length - 1;

                        if (i >= 0) {
                            for (; i >= 0; i--) {
                                var cur = $scope.fixture.ProcessingEntries[i];

                                // if the entry is new, add it a the beginning (we will sort the list later)
                                if (new Date(cur.Timestamp) < new Date(value.Timestamp)) {
                                    $scope.fixture.ProcessingEntries.unshift(value);
                                    break;
                                }

                                // if we have the same timestamp, then we need to update the existing
                                // entry instead of adding it
                                if (cur.Timestamp == value.Timestamp) {
                                    $.extend(cur, value);
                                    break;
                                }
                            }
                        }
                        else {
                            $scope.fixture.ProcessingEntries.unshift(value);
                        }
                    });

                    $scope.fixture.FillData();
                }
            });

        }]);   
})();
