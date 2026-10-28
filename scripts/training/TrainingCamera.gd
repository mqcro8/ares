## Scene-local vertical tracking; no dependency on briefing or gameplay state.
extends Camera2D

@export var target_path: NodePath
@export var minimum_y: float = 300.0
@export var maximum_y: float = 780.0
@export var vertical_offset: float = 20.0

@onready var _target: Node2D = get_node_or_null(target_path) as Node2D

func _ready() -> void:
	if _target == null:
		set_physics_process(false)
		return
	_update_position()
	reset_smoothing()

func _physics_process(_delta: float) -> void:
	if not is_instance_valid(_target):
		set_physics_process(false)
		return
	_update_position()

func _update_position() -> void:
	global_position.y = clampf(_target.global_position.y + vertical_offset, minimum_y, maximum_y)
