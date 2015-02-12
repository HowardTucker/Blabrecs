'use strict';

angular.module('BlabrecsApp')
  .controller('HeaderCtrl', ['$scope', 'User', function ($scope, User) {
      $scope.user = User.getUserData();
  }]);