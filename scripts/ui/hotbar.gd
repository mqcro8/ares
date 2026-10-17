extends Control

@onready var player : Player = get.tree().get_first_node_in_group("player")
var hand_equip

@onready var grid_container : GridContainer = $GridContainer 

func _ready()
	if(player):
		hand_equip = player.find_child("HandEquip")
		
		for button in grid_container.get_children():
			if(button is ItemButton):
				button.hand_equip= hand_equip
