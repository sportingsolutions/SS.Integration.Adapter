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
        port: '9000',
        uiUrlBase: '/ui/',

        pushNotification: {
            url: 'http://localhost',
            port: '9000',
            path: '/streaming',
            hub: 'SupervisorStreamingHub',
            enabled: true,
            sportGroupPath: 'SportGroup-:sportCode',
            fixtureGroupPath: 'FixtureGroup-:fixtureId',

            events: {
                AdapterUpdate: 'on-adapter-update-received',
                FixtureUpdate: 'on-fixture-update-received',
                SportUpdate: 'on-sport-update-received',
                Errors: 'on-error-received',
            },

            serverCallbacks: {
                AdapterUpdate: 'OnAdapterUpdate',
                FixtureUpdate: 'OnFixtureUpdate',
                SportUpdate: 'OnSportUpdate',
                Errors: 'OnError',
            },

            serverProcedures: {
                JoinSportGroup: 'JoinSportGroup',
                JoinFixtureGroup: 'JoinFixtureGroup',
                JoinAdapterGroup: 'JoinAdapterGroup',
                LeaveSportGroup: 'LeaveSportGroup',
                LeaveFixtureGroup: 'LeaveFixtureGroup',
                LeaveAdapterGroup: 'LeaveAdapterGroup',
            },
        },

        relations: {
            SportList: '/api/supervisor/sports',
            SportDetail: '/api/supervisor/sports/:sportCode/',
            FixtureDetail: '/api/supervisor/fixture/:fixtureId/details',
            FixtureHistory: '/api/supervisor/fixture/:fixtureId/history',
            FixtureSearch: '/api/supervisor/search/fixture',
            AdapterDetails: '/api/supervisor/details',
        },

        uiRelations: {
            Home: '/ui/index.html',
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

    /**
     * We modelled our config as a service so it can be easily accessed
     * whenever it is needed
     */
    services.constant('MyConfig', myConfig);

    /** 
     * This is the streaming service: it receives push notifications from the adapter's supervisor.
     *
     * A client can subscribe to three different types of notifications:
     *
     * 1) sport notifications - receives updates regarding a specific sport: use sportSubscription(sportCode).subscribe/unsubscribe
     * 2) fixture notifications - receives updates regarding a specific fixture: user fixtureSubscription(fixtureId).subscribe/unsubscribe
     * 3) adapter notifications - receives updates regarding the adapter internal state and possibile raised errors
     *
     * Use the specific "subscribe" methods in order to {un}-subscribe. Internally, the service sends broadcast messages 
     * (see MyConfig.pushNotifications.events) when a message is received from the server. 
     *
     * To use the service, a client should 1) subscribe itself to the service and 2) register specific listeners on $rootScope
     * to receive the desired broadcasted message.
     */
    services.factory('Streaming', ['$rootScope', '$log', '$q', 'MyConfig', function ($rootScope, $log, $q, MyConfig) {

        function signalRHubProxyFactory(url, hubname) {

            var defered = $q.defer();
            var promise = defered.promise;

            var connection = $.hubConnection(url);
            var proxy = connection.createHubProxy(hubname);
            var connected = false;

            connection.start()
                .done(function () { $log.info('Successfully connected to the streaming server'); connected = true; defered.resolve(); })
                .fail(function (error) { $log.error('Not connected to the streaming server: ' + error); MyConfig.pushNotification.enabled = false; connected = false; defered.reject(); });


            var on = function (eventName, callback) { if (MyConfig.pushNotification.enabled) { proxy.on(eventName, function (data) { $rootScope.$apply(function () { if (callback) callback(data); }); }); } };

            var invoke = function (methodName, params, callback) {

                // we need to use defer/promise here as at the time
                // the first subscribe() call is made, the connection
                // might not be yet ready

                var tmp = function () {
                    if (MyConfig.pushNotification.enabled) {
                        var res = null;

                        // if the RPC method doesn't accept any argument,
                        // we need to call invoke without any argument
                        // otherwise it won't work (even if we pass null)
                        if (params !== null) res = proxy.invoke(methodName, params);
                        else res = proxy.invoke(methodName);

                        res.done(function (data) { if (callback) callback(data); });
                    }
                };

                if (!connected)  promise.then(tmp);
                else tmp();
            };

            // register listeners and dispatch broadcast messages upon messages arrivals
            on(MyConfig.pushNotification.serverCallbacks.AdapterUpdate, function (data) { $rootScope.$broadcast(MyConfig.pushNotification.events.AdapterUpdate, data); });
            on(MyConfig.pushNotification.serverCallbacks.Errors, function (data) { $rootScope.$broadcast(MyConfig.pushNotification.events.Errors, data); });
            on(MyConfig.pushNotification.serverCallbacks.FixtureUpdate, function (data) { $rootScope.$broadcast(MyConfig.pushNotification.events.FixtureUpdate, data); });
            on(MyConfig.pushNotification.serverCallbacks.SportUpdate, function (data) { $rootScope.$broadcast(MyConfig.pushNotification.events.SportUpdate, data); });

            var rtn = {

                sportSubscription: function (sportCode) {

                    if (!sportCode || 0 === sportCode.length) return null;

                    return {
                        subscribe:   function () { invoke(MyConfig.pushNotification.serverProcedures.JoinSportGroup, sportCode, function () { $log.debug("Correctly subscribed to sport=" + sportCode); }); },
                        unsubscribe: function () { invoke(MyConfig.pushNotification.serverProcedures.LeaveSportGroup, sportCode, function () { $log.debug("Correctly un-subscribed from sport=" + sportCode); });},
                    };
                },

                fixtureSubscription: function (fixtureId) {

                    if (!fixtureId || 0 === fixtureId.length) return null;

                    return {
                        subscribe:   function () { invoke(MyConfig.pushNotification.serverProcedures.JoinFixtureGroup, fixtureId, function () { $log.debug("Correctly subscribed to fixtureId=" + fixtureId); }); },
                        unsubscribe: function () { invoke(MyConfig.pushNotification.serverProcedures.LeaveFixtureGroup, fixtureId, function () { $log.debug("Correctly un-subscribed from fixtureId=" + fixtureId); }); },
                    };
                },

                adapterSubscription: function () {
                    return {
                        subscribe:   function () { invoke(MyConfig.pushNotification.serverProcedures.JoinAdapterGroup, null, function () { $log.debug("Correctly subscribed to adapter's notifications"); }); },
                        unsubscribe: function () { invoke(MyConfig.pushNotification.serverProcedures.LeaveAdapterGroup, null, function () { $log.debug("Correctly un-subscribed from adapter's notifications");});},
                    };
                }
            };

            return rtn;
        };

        return signalRHubProxyFactory(MyConfig.fn.buildPath(MyConfig.pushNotification.path, MyConfig.pushNotification), MyConfig.pushNotification.hub);
    }]);

    /**
     * This is the supervisor service: it allows to query the adapter's supervisor about its internal state.
     *
     * Internally, it uses the angularjs' promise API - each methods (except getConfig and getStreamingService that return
     * respectively the MyConfig ang Streaming service) returns a promise. The caller should use the .then() method
     * to execute a callback when the data is available.
     *
     */
    services.factory('Supervisor', ['$http', '$log', '$q', 'Streaming', 'MyConfig', function ($http, $log, $q, Streaming, MyConfig) {

        return {

            /**
             * Returns the config object
             * @return {!Object}
             */
            getConfig: function () { return MyConfig; },

            /**
             * Allows to obtain a reference to the Streaming service.
             * The streaming service can also be obtained using
             * the standard angularjs's way
             * @return {!Object}
             */
            getStreamingService: function () { return Streaming; },

            /** 
             * Gets the adapter's details
             * @return {angular.$q.Promise}
             */
            getAdapterDetails: function () {

                var path = MyConfig.fn.buildPath(MyConfig.relations.AdapterDetails, MyConfig);

                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) { $log.debug("Adapter details correctly retrieved"); deferred.resolve(data); })
                    .error(function () { $log.error("An error occured while retrieving adapter details"); deferred.resolve({}); });

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
                    .success(function (data) { $log.debug("List of sports correctly retrieved"); deferred.resolve(data); })
                    .error(function () { $log.error("An error occured while retrieving the list of sports"); deferred.resolve({}); });

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
                    .success(function (data) { $log.debug('Sport details correctly retrieved'); deferred.resolve(data); })
                    .error(function () { $log.error('An error occured while retrieving sport details'); deferred.resolve({}); });

                return deferred.promise;
            },

            /**
             * Gets the fixture's detail
             * @param {string} fixtureId
             * @return {angular.$q.Promise}
             */
            getFixtureDetail: function (fixtureId) {

                if (!fixtureId || 0 === fixtureId.length) return null;

                var path = MyConfig.relations.FixtureDetail;
                path = MyConfig.fn.getFixturePath(path, fixtureId);
                path = MyConfig.fn.buildPath(path, MyConfig);

                $log.debug('Requesting fixture details for fixtureId=' + fixtureId);
                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) { $log.debug('Fixture details correctly retrieved'); deferred.resolve(data); })
                    .error(function () { $log.error('An error occured while retrieving fixture details'); deferred.resolve({}); });

                return deferred.promise;
            },

            /**
             * Gets the fixture's history
             * @param {string} fixtureId
             * @return {angular.$q.Promise}
             */
            getFixtureHistory: function (fixtureId) {

                if (!fixtureId || 0 === fixtureId.length) return null;

                var path = MyConfig.relations.FixtureHistory;
                path = MyConfig.fn.getFixturePath(path, fixtureId);
                path = MyConfig.fn.buildPath(path, MyConfig);

                $log.debug('Requesting fixture history for fixtureId=' + fixtureId);
                var deferred = $q.defer();
                $http.get(path)
                    .success(function (data) { $log.debug('Fixture history correctly retrieved'); deferred.resolve(data); })
                    .error(function () { $log.error('An error occured while retrieving fixture history'); deferred.resolve({}); });

                return deferred.promise;
            },

            searchFixture: function (searchData) {

                if (!searchData || 0 === searchData.length) return null;

                var path = MyConfig.relations.FixtureSearch;
                path = MyConfig.fn.buildPath(path, MyConfig);

                var deferred = $q.defer();
                $http.post(path, searchData)
                    .success(function (data) { deferred.resolve(data); })
                    .error(function () { $log.error('An error occured while searching for fixtures'); deferred.resolve({}); });

                return deferred.promise;
            }
        };
    }]);

})();