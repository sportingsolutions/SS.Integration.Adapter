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

    var models = {

        SportDetail: function SportDetail() {

            this.Name = "";
            this.CssClass = "";
            this.Url = "";
            this.Fixtures = {},
            this.InErrorState = 0,
            
            // don't rename this to Fixtures, otherwise extend doesn't work properly
            this.FixtureGroups = {
                InPlay: {
                    InErrorState: 0,
                    Items: new Array(),
                },
                InPreMatch: {
                    InErrorState: 0,
                    Items: new Array(),
                },
                InSetup: {
                    InErrorState: 0,
                    Items: new Array(),
                }
            };

            this.groupFixtures = function () {

                var outer = this;

                $.each(this.Fixtures, function (index, value) {
                    var group = null;
                    switch (value.State) {
                        // 0: setup - 1: Ready
                        case 0:
                        case 1:
                            group = outer.FixtureGroups.InSetup;
                            break;
                            // 2: pre-match
                        case 2:
                            group = outer.FixtureGroups.InPreMatch;
                            break;
                            // 3: in-play, 4: match over
                        case 3:
                        case 4:
                            group = outer.FixtureGroups.InPlay;
                            break;
                        default:
                            break;
                    }

                    if (group) {
                        if (value.IsInErrorState) group.InErrorState++;
                        group.Items.push(value);
                    }

                    if (value.IsInErrorState)
                        outer.InErrorState++;

                });
            }
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
            this.LastException = "";
            this.Sequence = "";
            this.ConnectionStatus = "";
            this.IsIgnored = false;
            this.IsDeleted = false;
            this.Epoch = "";
            this.EpochChangeReason = "";
        },
    };

    var fn = {
    
        GetSportsDetails : function(array, config)
        {
            if (!array)
                return new Array();

            if(!Array.isArray(array))
                array = [array];

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

    controllers.controller('AdapterCtrl', ['$scope', '$rootScope', 'Supervisor',
        function ($scope, $rootScope, Supervisor) {

            $rootScope.$broadcast('my-loading-started');

            $scope.details = {};

            var promise = Supervisor.getAdapterDetails();
            if (!promise)
                return;

            promise['finally'](function () {
                $rootScope.$broadcast('my-loading-complete');
            });

            promise.then(function (data) {
                $scope.details = data;
            });

        }]);

    controllers.controller('SportListCtrl', ['$scope', '$routeParams', '$rootScope', 'Supervisor',
        function ($scope, $routeParams, $rootScope, Supervisor) {

            $scope.sports = [];

            $rootScope.$broadcast('my-loading-started');

            var promise = Supervisor.getListOfSports();

            promise['finally'](function () {
                $rootScope.$broadcast('my-loading-complete');
            });

            promise.then(function (data) {
                $scope.sports = fn.GetSportsDetails(data, Supervisor.getConfig());
            });

        }]);

    controllers.controller('SportDetailCtrl', ['$scope', '$routeParams', '$rootScope', '$location', 'Supervisor',
        function ($scope, $routeParams, $rootScope, $location, Supervisor) {

            if (!$routeParams.hasOwnProperty("sportCode")) {
                $location.path(Supervisor.getConfig().uiRelations.Home);
            }

            $scope.sport = new models.SportDetail();
            $scope.tab = "all";
            $scope.search = null;

            $scope.changeTab = function (tab) {
                $scope.tab = tab;
            };

            $scope.getFixturesForTab = function(){

                if(!$scope.tab)
                    $scope.tab = 'all';

                if ($scope.search == "")
                    $scope.search = null;

                var fixtures = null;
                switch ($scope.tab) {
                    case 'in-play':
                        fixtures = $scope.sport.FixtureGroups.InPlay.Items;
                        break;
                    case 'in-prematch':
                        fixtures = $scope.sport.FixtureGroups.InPreMatch.Items;
                        break;
                    case 'in-setup':
                        fixtures = $scope.sport.FixtureGroups.InSetup.Items;
                        break;
                    default:
                        fixtures = $scope.sport.Fixtures;
                        break;
                }

                if ($scope.search) {
                    fixtures = fn.SearchFixtures(fixtures, $scope.search);
                }

                return fixtures;
            };

            $scope.getInErrorFixtureCountForTab = function() {

                if(!$scope.tab)
                    $scope.tab = 'all';

                switch ($scope.tab) {
                    case 'in-play':
                        return $scope.sport.FixtureGroups.InPlay.InErrorState;
                    case 'in-prematch':
                        return $scope.sport.FixtureGroups.InPreMatch.InErrorState;
                    case 'in-setup':
                        return $scope.sport.FixtureGroups.InSetup.InErrorState;
                }

                return $scope.sport.InErrorState;
            }

            $scope.openFixtureDetails = function (fixtureId) {
                
                if (!fixtureId)
                    return;

                var config = Supervisor.getConfig();
                var path = config.uiRelations.FixtureDetail;
                path = config.fn.getFixturePath(path, fixtureId);

                $location.path(path);
            }


            var promise = Supervisor.getSportDetail($routeParams.sportCode);
            if (!promise) {
                $location.path(Supervisor.getConfig().uiRelations.Home);
            }

            $rootScope.$broadcast('my-loading-started');

            promise['finally'](function () {
                $rootScope.$broadcast('my-loading-complete');
            });

            promise.then(function (data) {

                var tmp = fn.GetSportsDetails(data, Supervisor.getConfig());
                if (tmp.length && tmp.length > 0)
                    $scope.sport = tmp[0];
            });

        }]);

    controllers.controller('FixtureDetailCtrl', ['$scope', '$routeParams', '$rootScope', '$location', 'Supervisor',
        function ($scope, $routeParams, $rootScope, $location, Supervisor) {

            if (!$routeParams.hasOwnProperty("fixtureId")) {
                $location.path(Supervisor.getConfig().uiRelations.Home);
            }

            $scope.fixture = new models.FixtureDetail();

            var promise = Supervisor.getFixtureDetail($routeParams.fixtureId);

            $rootScope.$broadcast('my-loading-started');

            promise['finally'](function () {
                $rootScope.$broadcast('my-loading-complete');
            });

            promise.then(function (data) {
                $.extend($scope.fixture, data);
            });

        }]);

    controllers.controller('FixtureHistoryCtrl', ['$scope', '$routeParams', '$rootScope', 'Supervisor',
       function ($scope, $routeParams, $rootScope, Supervisor) {

           /*$rootScope.$broadcast('my-loading-started');

           var promise = Supervisor.getListOfSports();

           promise['finally'](function () {
               $rootScope.$broadcast('my-loading-complete');
           });

           promise.then(function (data) {
               $scope.sports = data;
           });*/

       }]);

   
})();
