@tool

extends Button

class_name ItemButton

@export var item: Item #esto se exporta de una "class" item con todas las items que pueden ir en el hotbar
	set(item_to_slot)
		item = item_to_slot
		icon = item.texture

var hand_equip : HandEquip

func _ready():
	if(not Engine.is_editor_hint()):
		connect("pressed", _on_pressed)		

fund _on_pressed():
	if(not Engine.is_editor_hint()):
		if(Item is EquipableItem)
			if(hand_equip != null):
				hand_equip.equipped_item = item
