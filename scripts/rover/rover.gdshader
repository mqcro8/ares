shader_type canvas_item;

uniform vec2 scroll_speed = vec2(0, 0);

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec2 scrolling_uv = fract(UV + TIME * scroll_speed);
    COLOR = texture(TEXTURE, scrolling_uv);
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
