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
                case 3: return 'In Play';
                case 4: return 'Over';
            }

            return 'Unknown status';
        };
    });

    app.filter('updateDescription', function () {
        return function (update) {

            if (!update) return "";

            var type = "Update";
            if (!update.IsUpdate) {
                type = "Full snapshot";
            }

            switch (update.State) {
                case 0:
                    return type + "/Processed";
                case 1:
                    return type + "/Processing";
                case 2:
                    return type + "/Skipped";
            }

            return type;
        }
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

    app.directive("myNotifications", ['$location', function ($location) {
        return {
            restrict: 'A',
            link: function (scope, element, attrs) {

                var limit = 10;  // max number of notifications to display
                var global = null;
                var globalCount = 0;

                scope.removeNotice = function (notice) {
                    var index = scope.noticies.indexOf(notice);
                    if (index > -1) scope.noticies.splice(index, 1);
                    notice.remove();
                };

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
                    buttons: { sticker:false, closer:false },
                    confirm: {
                        confirm: true,
                        buttons: [{
                            text: "Show me", addClass: "btn-danger",
                            click: function (notice) {
                                if (!notice || !notice.fixtureId) return;
                                $location.path('/ui/fixture/' + notice.fixtureId);
                                scope.removeNotice(notice);
                                scope.$apply();
                            }
                        }, {
                            text:"Cancel", 
                            click: function (notice) { scope.removeNotice(notice); }
                        }]
                    }
                };

                var globalOpts = {
                    title: "Something went wrong...",
                    stack: stack_context,
                    type: "error",
                    width: "100%",
                    hide: false,
                    buttons: { sticker: false }
                };

                scope.$on('on-error-notification-received', function (evt, args) {
                    opts.text = "<p>On <i>" + args.FixtureDescription + "<i></p>";
                    opts.text += "<p>" + args.Message + "</p>";
                    
                    if(scope.noticies === undefined) scope.noticies = new Array();

                    globalCount++;
                    if (scope.noticies.length < limit) {
                        var notice = new PNotify(opts);
                        notice.fixtureId = args.FixtureId;
                        scope.noticies.push(notice);
                    }
                    else {
                        if (global !== null) global.remove();
                        globalOpts.text = "There are more than " + globalCount + " errors";
                        global = new PNotify(globalOpts);
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
    }]);

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
