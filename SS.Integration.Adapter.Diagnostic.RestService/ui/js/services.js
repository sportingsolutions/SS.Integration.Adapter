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

    /** @type {ssln.supervisor.config} */
    var myConfig = {
        url: 'http://localhost',
        port: '58623',
        uiUrlBase: '/ui/',

        // set as {} to disable push notifications
        pushNotification: {
            server : 'http://localhost',
            port: '58623',
            path: '/streaming'
        },

        relations: {
            SportList: '/api/supervisor/sports',
            SportDetail: '/api/supervisor/sports/:sportCode/',
            FixtureDetail: '/api/supervisor/fixture/:fixtureId/details',
            FixtureHistory: '/api/supervisor/fixture/:fixtureId/history',
            AdapterDetails: '/api/supervisor/details'
        },

        uiRelations: {
            Home:'/ui/index.html',
            SportList: '/ui/sports/',
            SportDetail: '/ui/sport/:sportCode',
            FixtureDetail: '/ui/fixture/:fixtureId/details',
            FixtureHistory: '/ui/fixture/:fixtureId/history'
        },

        fn: {

            /**
             * Creates a full url given a path
             * @param {!string} path
             * @param {!ssln.supervisor.config} config
             */
            buildPath: function (path, config) {

                if (path.indexOf('/') != 0) path = '/' + path;

                return [config.url, ':', config.port, path].join('');
            },

            getSportPath: function (path, sport) {
                return path.replace(/:sportCode/g, sport);
            },

            getFixturePath: function (path, fixtureId) {
                return path.replace(/:fixtureId/g, fixtureId);
            }
        }
    };


    var services = angular.module('adapterSupervisorServices', []);

    services.constant('MyConfig', myConfig);

    services.factory('Streaming', ['$rootScope', 'MyConfig', function ($rootScope, MyConfig) {
        
        function signalRHubProxyFactory(url, hubname) {

            var connection = $.hubConnection(url);
            var proxy = connection.createHubProxy(hubname);
            connection.start().done(function () { });

            return {

                onSportUpdate: function (sport, callback) {
                    proxy.on(sport, function (data) {
                        $rootScope.$apply(function () {
                            if (callback)
                                callback(data);
                        });
                    });
                },

                off: function (eventName, callback) {
                    proxy.off(eventName, function (data) {
                        $rootScope.$apply(function () {
                            if (callback)
                                callback(data);
                        });
                    });
                },

                invoke: function (methodName, callback) {
                    proxy.invoke(methodName).done(function (data) {
                        $rootScope.$apply(function () {
                            if (callback)
                                callback(data);
                        });
                    });
                },
            };
        };

        return signalRHubProxyFactory;
    }]);

    services.factory('Supervisor', ['$http', '$log', '$q', 'MyConfig', function ($http, $log, $q, MyConfig) {

        return {

            /**
             * Returns the config object
             * @return {!Object}
             */
            getConfig: function() {
                return MyConfig;
            },

            /** 
             * Gets the adapter's details
             * @return {angular.$q.Promise}
             */
            getAdapterDetails: function () {

                var path = MyConfig.fn.buildPath(MyConfig.relations.AdapterDetails, MyConfig);

                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) {
                        $log.debug("Adapter details correctly retrieved");
                        deferred.resolve(data);
                    })
                    .error(function () {
                        $log.error("An error occured while retrieving adapter details");
                        deferred.resolve({});
                    });

                return deferred.promise;
            },

            /**
             * Get the list of sports currently known by the adapter
             * @return {angular.$q.Promise}
             */
            getListOfSports: function () {

                var path = MyConfig.fn.buildPath(MyConfig.relations.SportList, MyConfig);

                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) {
                        $log.debug("List of sports correctly retrieved");
                        deferred.resolve(data);
                    })
                    .error(function () {
                        $log.error("An error occured while retrieving the list of sports");
                        deferred.resolve({});
                    });

                return deferred.promise;
            },

            /** 
             * Get the details associated to the given sport
             * @param {!string} sportCode
             * @return {angular.$q.Promise}
             */
            getSportDetail: function (sportcode) {

                if (!sportcode || 0 === sportcode.length)
                    return null;

                var path = MyConfig.relations.SportDetail;
                path = MyConfig.fn.getSportPath(path, sportcode);
                path = MyConfig.fn.buildPath(path, MyConfig);

                $log.debug('Requesting sport details for sport=' + sportcode);
                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) {
                        $log.debug('Sport details correctly retrieved');
                        deferred.resolve(data);
                    })
                    .error(function () {
                        $log.error('An error occured while retrieving sport details');
                        deferred.resolve({});
                    });

                return deferred.promise;
            },

            /**
             * Gets the fixture's detail
             * @param {string} fixtureId
             * @return {angular.$q.Promise}
             */
            getFixtureDetail: function (fixtureId) {

                if (!fixtureId || 0 === fixtureId.length)
                    return null;

                var path = MyConfig.relations.FixtureDetail;
                path = MyConfig.fn.getFixturePath(path, fixtureId);
                path = MyConfig.fn.buildPath(path, MyConfig);

                $log.debug('Requesting fixture details for fixtureId=' + fixtureId);
                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) {
                        $log.debug('Fixture details correctly retrieved');
                        deferred.resolve(data);
                    })
                    .error(function () {
                        $log.error('An error occured while retrieving fixture details');
                        deferred.resolve({});
                    });

                return deferred.promise;
            },

            /**
             * Gets the fixture's history
             * @param {string} fixtureId
             * @return {angular.$q.Promise}
             */
            getFixtureHistory: function (fixtureId) {

                if (!fixtureId || 0 === fixtureId.length)
                    return null;

                var path = MyConfig.relations.FixtureHistory;
                path = MyConfig.fn.getFixturePath(path, fixtureId);
                path = MyConfig.fn.buildPath(path, MyConfig);

                $log.debug('Requesting fixture history for fixtureId=' + fixtureId);
                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) {
                        $log.debug('Fixture history correctly retrieved');
                        deferred.resolve(data);
                    })
                    .error(function () {
                        $log.error('An error occured while retrieving fixture history');
                        deferred.resolve({});
                    });

                return deferred.promise;
            }
        };
    }]);

})();