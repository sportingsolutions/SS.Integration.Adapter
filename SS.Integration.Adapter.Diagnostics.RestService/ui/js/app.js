/* Adapter Supervisor Module  */

//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

'use strict';

(function () {

    var app = angular.module('adapterSupervisorApp', [
        'ngRoute',
        'adapterSupervisorControllers',
        'adapterSupervisorServices'
    ]);

    app.filter('bytes', function () {
        return function (bytes, precision) {
            if (isNaN(parseFloat(bytes)) || !isFinite(bytes)) return '-';
            if (typeof precision === 'undefined') precision = 1;
            var units = ['bytes', 'kB', 'MB', 'GB', 'TB', 'PB'],
                number = Math.floor(Math.log(bytes) / Math.log(1024));
            return (bytes / Math.pow(1024, Math.floor(number))).toFixed(precision) + ' ' + units[number];
        }
    });

    app.filter('fixtureStatus', function () {
        return function (status) {
            switch (status) {
                case 0: return 'In Setup';
                case 1: return 'Ready';
                case 2: return 'PreMatch';
                case 3: return 'Running';
                case 4: return 'Over';
            }

            return 'Unknown status';
        };
    });

    /**
     * Allows to display a "waiting" (modal) layer
     * 
     * Brodcast 
     * 1) "my-loading-started"  to show it
     * 2) "my-loading-complete" to hide it
     *
     */
    app.directive("myLoadingIndicator", function () {
        return {
            restrict: 'A',
            link: function (scope, element) {
                scope.$on('my-loading-started',  function () { element.css({ "display": "block" }); });
                scope.$on('my-loading-complete', function () { element.css({ "display": "none" }); });
            },
        };
    });

    app.directive("myNotifications", function () {
        return {
            restrict: 'A',
            link: function (scope, element, attrs) {

                var limit = 10;  // max number of notifications to display
                var global = null;
                var globalCount = 0;

                var stack_context = {
                    "dir1": "right",
                    "dir2": "down",
                    "push": "top",
                    "firstpos2": 50,
                    "spacing2": 10,
                    "context": $("#" + attrs.myNotifications)
                }

                var opts = {
                    title: "Something went wrong...",
                    stack: stack_context,                    
                    type: "error",
                    width: "100%",
                    hide: false,
                    buttons: { sticker:false },
                    confirm: {
                        confirm: true,
                        buttons: [{ text: "Show me", addClass: "btn-danger",
                            click: function (notice) {
                                // TODO redirect here using $locationProvider
                                // TODO remove notice from noticies list
                            }
                        }]
                    }
                };

                scope.$on('on-error-notification-received', function (evt, args) {
                    opts.text = args.text;
                    if(scope.noticies === undefined) scope.noticies = new Array();

                    globalCount++;
                    if(scope.noticies.length < limit) scope.noticies.push(new PNotify(opts));
                    else {
                        if (global !== null) global.remove(); 
                        opts.text = "There are more than " + globalCount + " errors";
                        global = new PNotify(opts);
                    }
                });

                scope.$on('on-error-notification-clear-all', function () {
                    $.each(scope.noticies, function (index, value) { value.remove(); });
                    scope.noticies.length = 0;
                    if (global !== null) global.remove();
                    global = null;
                    globalCount = 0;
                });
            },
        };
    });

    app.config(['$routeProvider', '$locationProvider', function ($routeProvider, $locationProvider) {
        $routeProvider.
            when('/ui/sports', {
                templateUrl: '/ui/partials/sports.html',
                controller: 'SportListCtrl',
                controllerAs: 'ctrl',
            }).
            when('/ui/sport/:sportCode', {
                templateUrl: '/ui/partials/sport.html',
                controller: 'SportDetailCtrl',
                controllerAs: 'ctrl',
            }).
            when('/ui/fixture/:fixtureId', {
                templateUrl: '/ui/partials/fixture.html',
                controller: 'FixtureDetailCtrl',
                controllerAs: 'ctrl',
            }).
            when('/ui/fixture/:fixtureId/details', {
                templateUrl: '/ui/partials/fixture.html',
                controller: 'FixtureDetailCtrl',
                controllerAs: 'ctrl',
            }).
            otherwise({
                redirectTo: function (routeParams, path, search) {
                    if (search && search.path && (search.path.indexOf("/ui/sport") > -1 || search.path.indexOf("/ui/fixture/") > -1)) { return search.path; }
                    return "/ui/sports";
                }
            });
        $locationProvider.html5Mode({ enabled: true, requireBase: false });
    }]);

})();
