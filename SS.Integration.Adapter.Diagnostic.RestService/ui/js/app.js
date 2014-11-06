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
        'ui.bootstrap',

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

    app.directive("myLoadingIndicator", function () {
        return {
            restrict: 'A',
            link: function (scope, element, attrs) {

                scope.$on('my-loading-started', function (e) {
                    element.css({ "display": "block" });
                });

                scope.$on('my-loading-complete', function (e) {
                    element.css({ "display": "none" });
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
            when('/ui/fixture/:fixtureId/history', {
                templateUrl: '/ui/partials/fixture.html',
                controller: 'FixtureHistoryCtrl',
                controllerAs: 'ctrl',
            }).
            otherwise({
                redirectTo: function (routeParams, path, search) {

                    if (search && search.path && (search.path.indexOf("/ui/sport") > -1 || search.path.indexOf("/ui/fixture/") > -1)) {
                        return search.path;
                    }

                    return "/ui/sports";
                }
            });
        $locationProvider.html5Mode({ enabled: true, requireBase: false });
    }]);

})();
