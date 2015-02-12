'use strict';

angular.module('BlabrecsApp')
  .controller('LogoutCtrl', ['$state', 'User', function ($state, User) {
      User.removeAuthentication();
      $state.go('main');
  }]);