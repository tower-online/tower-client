class_name MainCamera extends Camera3D

@export var distance: float = 10.0
@export_range(0, 89) var angle: float = 45.0
var target: Node3D	


func _process(_delta: float) -> void:
	if not target:	
		return
		
	var h := distance * sin(deg_to_rad(angle))
	var w := distance * cos(deg_to_rad(angle))
	
	position = target.position + Vector3(0, h, w)
	look_at(target.position)
