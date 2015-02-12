/*global jQuery:true */
'use strict';

angular.module('BlabrecsApp')
  .controller('RegisterCtrl', ['$scope', '$state', 'User', function ($scope, $state, User) {
      $scope.username = '';
      $scope.password = '';
      $scope.confirmPassword = '';
      $scope.persist = false;
      $scope.errors = [];
      var nextState = null;

      try {
          nextState = User.getNextState();
      } catch (e) {
          nextState = null;
      }

      if (nextState !== null) {
          var nameBuffer = nextState.name + '';
          var errorBuffer = nextState.error + '';
          User.clearNextState();
          nextState = {
              name: nameBuffer,
              error: errorBuffer
          };
          if (typeof nextState.error === 'string' && nextState.error !== '' && $scope.errors.indexOf(nextState.error) === -1) {
              $scope.errors.push(nextState.error);
          } else {
              $scope.errors.push('You must be logged in to view this page');
          }
      }

      function disableRegisterButton(message) {
          if (typeof message !== 'string') {
              message = 'Attempting register...';
          }
          jQuery('#register-form-submit-button').prop('disabled', true).prop('value', message);
      }

      function enableRegisterButton(message) {
          if (typeof message !== 'string') {
              message = 'Submit';
          }
          jQuery('#register-form-submit-button').prop('disabled', false).prop('value', message);
      }

      function onSuccessfulRegister() {
          if (nextState !== null && typeof nextState.name === 'string' && nextState.name !== '') {
              $state.go(nextState.name, nextState.params);
          } else {
              User.authenticate($scope.username, $scope.password, function () { $state.go('games'); }, function () { }, true);
          }
      }

      function onFailedRegister(error) {
          if (typeof error === 'string' && $scope.errors.indexOf(error) === -1) {
              $scope.errors.push(error);
          }
          enableRegisterButton();
      }

      $scope.register = function () {
          disableRegisterButton();
          User.register($scope.username, $scope.password, $scope.confirmPassword, onSuccessfulRegister, onFailedRegister, $scope.persist);
      };
  }]);