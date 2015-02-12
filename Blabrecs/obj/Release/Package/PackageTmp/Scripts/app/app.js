'use strict';

(function () {
    angular.module('BlabrecsApp', [
      'ngCookies',
      'ngResource',
      'ngSanitize',
      'ngRoute',
      'ui.router'
    ])
      .config(function ($stateProvider, $locationProvider) {
          $locationProvider.html5Mode(true);

          $stateProvider
            .state('register',
            {
                templateUrl: 'Views/register.html',
                controller: 'RegisterCtrl'
            })
            .state('login',
            {
                templateUrl: 'Views/login-form.html',
                controller: 'LoginCtrl'
            })
            .state('logout',
            {
                controller: 'LogoutCtrl'
            })
            .state('games', {
                url: '/',
                templateUrl: 'Views/games.html',
                controller: 'GamesCtrl',
                resolve: {
                    user: 'User',
                    authenticationRequired: function (user) {
                        user.isAuthenticated();
                    }
                }
            });
      })
      .run(function ($rootScope, $state, User) {
          try {
              User.isAuthenticated();
          } catch (e) {
              // do nothing with this error
          }
          $rootScope.$on('$stateChangeError', function (event, toState, toParams, fromState, fromParams, error) {
              if (error.name === 'AuthenticationRequired') {
                  User.setNextState(toState.name, 'You must login to access this page');
                  $state.go('login', {}, { reload: true });
              }
          });
      })
        .value('game', $.connection.game);
})();