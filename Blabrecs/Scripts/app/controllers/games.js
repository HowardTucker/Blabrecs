'use strict';

angular.module('BlabrecsApp')
  .controller('GamesCtrl', ['$scope', '$resource', 'game', 'User', function ($scope, $resource, game, User) {
      $scope.name = "New Game"; // default value of the game name box
      $scope.numberOfPlayers = 4;
      $scope.message = ""; // default value of the chat box
      $scope.messages = []; // list of messages
      $scope.players = []; // list of players
      $scope.inGame = false; // Used to toggle state between available game list and being in a game
      // switch view back to open games list
      game.client.onAbandonGame = function () {
          $scope.inGame = false;
          $scope.$apply();
      };
      // get list of open games
      game.client.onActiveGames = function (data) {
          $scope.games = data;
          $scope.$apply();
      };
      // display message informing player that the logged in player is already in that game
      game.client.onAlreadyInGame = function (data) {
          $scope.alreadyInGame = true;
          $scope.$apply();
      };
      // close the game, switch to open games list
      game.client.onCloseGame = function () {
          $scope.closedGame = true;
          $scope.inGame = false;
          $scope.$apply();
      };
      // Display new chat message in game chat
      game.client.onDisplayMessage = function (newMessage) {
          $scope.messages.push({ message: newMessage.message, user: newMessage.user });
          $scope.$apply();
          $('.scrollable').stop().animate({
              scrollTop: $(".scrollable")[0].scrollHeight
          }, 800);
      };
      // this is to request a new gamestate
      game.client.onGameStateUpdated = function () {
          game.server.getGameState($scope.gameState.Id);
      };
      // join a game
      game.client.onJoinGame = function (game, players) {
          $scope.players = players;
          $scope.inGame = true;
          $scope.gameOwner = false;
          $scope.alreadyInGame = false;
          $scope.gameState = { "Id": game };
          $scope.gameState.board = ConvertLettersToArray({});
          $scope.$apply();
          BindDragAndDrop();
          $scope.$apply();
      }
      // update players when someone joines the game you are currently in
      game.client.onPlayerJoins = function (player) {
          $scope.players.push(player);
          if ($scope.players.length >= 2) {
              $scope.canStart = true;
          } else {
              $scope.canStart = false;
          }
          $scope.$apply();
      }
      // update players when a player leaves
      game.client.onPlayerLeaves = function (player) {
          for (var i = 0; i < $scope.players.length; i++) {
              if ($scope.players[i].name === player.name) {
                  $scope.players.splice(i, 1);
                  if ($scope.players.length >= 2) {
                      $scope.canStart = true;
                  } else {
                      $scope.canStart = false;
                  }
              }
          }
          $scope.$apply();
      };
      // start a game
      game.client.onStartGame = function () {
          game.server.getGameState($scope.gameState.Id);
          $scope.gameState.gameStarted = true;
      };
      // create a game
      game.client.onSwitchToGame = function (game, player) {
          $scope.waitingForPlayers = true;
          $scope.gameOwner = true;
          $scope.canStart = false;
          $scope.alreadyInGame = false;
          $scope.players = [];
          $scope.players.push(player);
          $scope.inGame = true;
          $scope.gameState = { "Id": game.id, "LobbyName": game.name };
          $scope.gameState.board = ConvertLettersToArray({});
          $scope.$apply();
          BindDragAndDrop();
          $scope.$apply();
      };
      // get and display the updated game state
      game.client.onUpdateGameState = function (rackLetters, letters, isTurn, players, gameOver) {
          $scope.gameState.gameMessages = [];
          if (gameOver) {
              $scope.gameState.isTurn = false;
              $scope.gameState.gameMessages.push("Game over!");
          } else {
              $scope.gameState.isTurn = isTurn;
          }
          $scope.gameState.rack = ConvertRackLettersToArray(rackLetters);
          $scope.gameState.board = ConvertLettersToArray(letters);
          $scope.players = players;
          $scope.$apply();
          BindDragAndDrop();
          $scope.$apply();
      }
      // display a validation error
      game.client.onValidationError = function (gameMessages) {
          $scope.gameState.isTurn = true;
          $scope.gameState.gameMessages = gameMessages;
          $scope.$apply();
      };
      $scope.abandonGame = function () {
          game.server.abandonGame($scope.gameState.Id, $scope.gameOwner);
      }
      $scope.createGame = function () {
          game.server.newGame($scope.name, $scope.numberOfPlayers);
      };
      $scope.joinGame = function (joinedGame) {
          game.server.joinGame(joinedGame.Id);
      };
      $scope.passTurn = function () {
          var letters = [];
          // get any letters selected to be discarded
          $.each($('.pass-checkbox:checked'), function () {
              letters.push({ Id: $(this).data('letter') });
          });
          game.server.passTurn($scope.gameState.Id, letters);
          $('#passModal').modal("hide");
      };
      $scope.sendMessage = function () {
          game.server.sendMessage($scope.gameState.Id, $scope.message)
          $scope.message = "";
      };
      $scope.startGame = function () {
          $scope.canStart = false;
          game.server.startGame($scope.gameState.Id);
      };
      $scope.validateGameState = function () {
          var letters = [];
          // get letters played by the player
          $.each($('.player-letter'), function () {
              if ($(this).parent().data('row') != null) {
                  letters.push({ Row: $(this).parent().data('row'), Column: $(this).parent().data('col'), Id: $(this).data('letter') });
              }
          });
          // do some clientside validation before submitting the turn
          // validation will be duplicated on the server for security
          var vertical = AllInColumn();
          var horizontal = AllInRow();
          if (vertical || horizontal) {
              $scope.gameState.isTurn = false;
              game.server.submitTurn($scope.gameState.Id, letters);
          }
      };
      // Bind the drag events for player letters and make the board and rack spaces droppable
      function BindDragAndDrop() {
          $('.draggable').bind('dragstart', function (event) {
              event.originalEvent.dataTransfer.setData("text", event.target.getAttribute('id'));
          });
          $('.board-space.isTurntrue, .letter-rack').bind('dragover', function (event) {
              event.preventDefault();
          });
          $('.board-space, .letter-rack').bind('drop', function (event) {
              var notecard = event.originalEvent.dataTransfer.getData("text");
              if ($(event.target).hasClass('draggable')) {
                  var parent = $(event.target).parent()[0];

                  $(document.getElementById(notecard)).parent()[0].appendChild(event.target);
                  parent.appendChild(document.getElementById(notecard));
              } else {
                  event.target.appendChild(document.getElementById(notecard));
              }
              event.preventDefault();
          });
      }
      // Convert the letters in the players rack into a length 7 array so that the empty rack spaces will be appear open
      function ConvertRackLettersToArray(letters) {
          var rack = new Array();
          for (var i = 0; i < 7; i++) {
              rack[i] = {};
          }
          for (var i = 0; i < letters.length; i++) {
              rack[letters[i].rackPosition - 1] = letters[i];
          }
          return rack;
      }
      // Convert the board played letters into a 15x15 array so that the board is properly constructed with open spaces
      function ConvertLettersToArray(letters) {
          var board = new Array();
          for (var i = 0; i < 15; i++) {
              board[i] = new Array();
              for (var j = 0; j < 15; j++) {
                  board[i][j] = { row: i, col: j, class: "" };
              }
          }
          for (var i = 0; i < letters.length; i++) {
              board[letters[i].row][letters[i].column] = { row: letters[i].row, col: letters[i].column, class: 'letter letter-' + letters[i].letterType };
          }
          return board;
      }
      // Check to see if all letters played by player are in the same row
      function AllInRow() {
          var rows = [];
          var firstRow = null;
          $.each($('.player-letter'), function () {
              rows.push($(this).parent().data('row'));
          });
          for (var i = 0; i < rows.length; i++) {
              if (rows[i] != undefined) {
                  if (firstRow === null) {
                      firstRow = rows[i];
                  } else {
                      if (rows[i] != firstRow) {
                          return false;
                      }
                  }
              }
          }
          return true;
      }
      // Check to see if all letters played by player are in the same column
      function AllInColumn() {
          var columns = [];
          var firstColumn = null;
          $.each($('.player-letter'), function () {
              columns.push($(this).parent().data('col'));
          });
          for (var i = 0; i < columns.length; i++) {
              if (columns[i] != undefined) {
                  if (firstColumn === null) {
                      firstColumn = columns[i];
                  } else {
                      if (columns[i] != firstColumn) {
                          return false;
                      }
                  }
              }
          }
          return true;
      }
      $(function () {
          $.connection.hub.logging = false;
          $.connection.hub.start();
      });

      $.connection.hub.error(function (err) {
          console.log('An error occurred: ' + err);
      });
  }]);