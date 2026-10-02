extends SceneTree

## Headless parse gate. Run via Tools/gdscript-check/check.py, not from play.
## Lives under addons/ so ResourceLoader can load it (headless --script).

const _ROOTS: PackedStringArray = ["res://addons", "res://addons_custom"]
## 第三方插件目录名。项目有 vendor 时往这里加，默认扫描会跳过。
const _VENDOR: PackedStringArray = []


func _initialize() -> void:
	var opts := _parse_args()
	var paths := _collect_scripts(opts)
	paths.sort()
	var failed: Array[String] = []
	for path in paths:
		print("GDSCRIPT_CHECK_BEGIN %s" % path)
		var loaded := ResourceLoader.load(path, "GDScript")
		if loaded == null:
			print("GDSCRIPT_CHECK_FAIL %s" % path)
			failed.append(path)
		else:
			print("GDSCRIPT_CHECK_OK %s" % path)
	var payload := {
		"checked": paths.size(),
		"ok": paths.size() - failed.size(),
		"failed": failed,
	}
	print("GDSCRIPT_CHECK_JSON:%s" % JSON.stringify(payload))
	quit(1 if failed.size() > 0 else 0)


func _parse_args() -> Dictionary:
	var include_all := false
	var include_vendor := false
	var roots: PackedStringArray = []
	var args := OS.get_cmdline_user_args()
	var i := 0
	while i < args.size():
		var token := args[i]
		if token == "--all":
			include_all = true
		elif token == "--vendor":
			include_vendor = true
		elif token == "--root" and i + 1 < args.size():
			i += 1
			roots.append(args[i])
		i += 1
	return {
		"all": include_all,
		"vendor": include_vendor,
		"roots": roots,
	}


func _collect_scripts(opts: Dictionary) -> PackedStringArray:
	var out: PackedStringArray = []
	var extra: PackedStringArray = opts["roots"]
	if extra.size() > 0:
		for root_path in extra:
			_collect_path(root_path, out)
		return out
	if opts["all"]:
		for root_path in _ROOTS:
			_walk(root_path, out)
		return out
	_walk_addons(bool(opts["vendor"]), out)
	return out


func _collect_path(path: String, out: PackedStringArray) -> void:
	if path.ends_with(".gd"):
		if not out.has(path):
			out.append(path)
		return
	_walk(path, out)


func _walk_addons(include_vendor: bool, out: PackedStringArray) -> void:
	for root_path in _ROOTS:
		var dir := DirAccess.open(root_path)
		if dir == null:
			continue
		dir.list_dir_begin()
		var name := dir.get_next()
		while name != "":
			if name != "." and name != ".." and dir.current_is_dir() and not name.begins_with("."):
				if include_vendor or not _VENDOR.has(name):
					_walk(root_path.path_join(name), out)
			name = dir.get_next()
		dir.list_dir_end()


func _walk(dir_path: String, out: PackedStringArray) -> void:
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return
	dir.list_dir_begin()
	var name := dir.get_next()
	while name != "":
		if name == "." or name == "..":
			name = dir.get_next()
			continue
		var child := dir_path.path_join(name)
		if dir.current_is_dir():
			if not name.begins_with("."):
				_walk(child, out)
		elif name.ends_with(".gd"):
			out.append(child)
		name = dir.get_next()
	dir.list_dir_end()
