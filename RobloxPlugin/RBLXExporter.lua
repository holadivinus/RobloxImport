-- Services
local Selection = game:GetService("Selection");


-- Plugin Vars
local toolbar = plugin:CreateToolbar("Save locally");
local pluginButton = toolbar:CreateButton("RBLX Export", "Export to Luas, for python finalizer", "rbxassetid://1507949215");


-- Usefull Functions
function SaveFile(text, filename)
	local Script = Instance.new("Script",game.Workspace);
	Script.Source = text;
	Script.Name = "SaveFile";
	Selection:Set({Script});
	plugin:PromptSaveSelection(filename);
	Script:Remove();
end

local MAX_SEGMENT_LENGTH = 199990
function saveSegmentedString(str)
	local segments = math.ceil(#str / MAX_SEGMENT_LENGTH)
	local startIndex = 1

	for i = 1, segments do
		local endIndex = math.min(startIndex + MAX_SEGMENT_LENGTH - 1, #str)
		local segment = string.sub(str, startIndex, endIndex)
		SaveFile(segment, "export" .. tostring(i))
		startIndex = endIndex + 1
	end
end


local function getAssetID(data)
	local str = tostring(data);
	local prefix1 = "http://www.roblox.com/asset/?id="
	local prefix2 = "rbxassetid://"

	if string.sub(str, 1, #prefix1) == prefix1 then
		return string.sub(str, #prefix1 + 1)
	elseif string.sub(str, 1, #prefix2) == prefix2 then
		return string.sub(str, #prefix2 + 1)
	else
		return str
	end
end

local allString = "";
local function add(data)
	allString = allString .. tostring(data);
end

-- Main plugin button func
local function onNewScriptButtonClicked()
	print("Started Serialization...");

	local AllObjects = game.Workspace:GetDescendants();
	allString = "";

	-- all objects (focus on parts & their types)
	for i,v in pairs(AllObjects) do
		local valid = false;

		-- current object type switch
		if (v:isA("Part")) then
			valid = true;
			add("_Part:");
			add("\n    Shape: " .. tostring(v.Shape.Value));
		elseif (v:isA("WedgePart")) then
			valid = true;
			add("_WedgePart:");
		elseif (v:isA("CornerWedgePart")) then
			valid = true;
			add("_CornerWedgePart:");
		elseif (v:isA("TrussPart")) then
			valid = true;
			add("_TrussPart:");
		end
		if (valid) then
			add("\n    Name: " .. tostring(v.Name));
			add("\n    Position: " .. tostring(v.Position));
			add("\n    Orientation: " .. tostring(v.Orientation));
			add("\n    Size: " .. tostring(v.Size));
			add("\n    BrickColor: " .. tostring(v.BrickColor.Number));
			add("\n    Material: " .. tostring(v.Material.Value));
			add("\n    Transparency: " .. tostring(v.Transparency));
			add("\n    CanCollide: " .. tostring(v.CanCollide));
			add("\n    Reflectance: " .. tostring(v.Reflectance));

			-- modifiers (mesh, decal, texture, etc)
			for	i,v2 in pairs(v:GetDescendants()) do
				-- current modifier type switch
				if (v2.Parent == v) then
					if (v2:isA("SpecialMesh")) then
						add("\n    SpecialMesh: {" .. tostring(v2.MeshType) .. '-TO-' .. getAssetID(v2.MeshId) .. '-TO-' .. getAssetID(v2.TextureId) .. '-TO-' .. tostring(v2.Scale).. '-TO-' .. tostring(v2.Offset) .. '-TO-' .. tostring(v2.VertexColor) .. '}');
					elseif (v2:isA("Decal")) then
						add("\n    Decal: {" .. tostring(v2.Color3) .. '-TO-' .. getAssetID(v2.Texture) .. '-TO-' .. tostring(v2.Transparency) .. '-TO-' .. tostring(v2.Face) .. '}');
					elseif (v2:isA("Texture")) then
						add("\n    Texture: {" .. tostring(v2.Color3) .. '-TO-' .. getAssetID(v2.Texture) .. '-TO-' .. tostring(v2.Transparency) .. '-TO-' .. tostring(v2.Face) .. '-TO-' .. tostring(v2.OffsetStudsU) .. '-TO-' .. tostring(v2.OffsetStudsV) .. '-TO-' .. tostring(v.StudsPerTileU) .. '-TO-' .. tostring(v.StudsPerTileV) .. '}');
					end
				end
			end
			add("\n");
		end
	end
	print("Done caching.");
	saveSegmentedString(allString);
end

-- make button exec above func
pluginButton.Click:Connect(onNewScriptButtonClicked);