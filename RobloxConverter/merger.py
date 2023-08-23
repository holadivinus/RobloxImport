import os
import re

# Get all files in the same directory
files = os.listdir()

# Filter out only .lua files in the format 'export{filenum}.lua'
lua_files = [file for file in files if re.fullmatch(r'export\d+\.lua', file)]

# Sort files based on filenum
lua_files.sort(key=lambda x: int(re.findall(r'\d+', x)[0]))

# Ensure the subfolder exists
os.makedirs('subfolder', exist_ok=True)


pending_ids = set()
def extract_text_between_brackets(string):
    pattern = r"\{(.*?)\}"  # Regular expression pattern to match text between curly brackets
    matches = re.findall(pattern, string)
    return matches[0]

print("merging luas into main txt...")
# Open the output file in write mode
with open('subfolder/output.roblox', 'w') as outfile:
    # Iterate through each .lua file
    for lua_file in lua_files:
        # Open the .lua file in read mode
        with open(lua_file, 'r') as infile:
            # Read the contents of the .lua file
            content = infile.read()

            # Write the contents of the .lua file to the output file
            outfile.write(content)

with open('subfolder/output.roblox', 'r') as outfile:
    content = outfile.read()

    lines = content.split('\n')
    for line in lines:
        stripped_line = line.strip()
        if stripped_line.startswith('SpecialMesh'):
            params = extract_text_between_brackets(stripped_line).split('-TO-')
            pending_ids.add(params[1])
            pending_ids.add(params[2])
        elif stripped_line.startswith('Decal'):
            params = extract_text_between_brackets(stripped_line).split('-TO-')
            pending_ids.add(params[1])
        elif stripped_line.startswith('Texture'):
            params = extract_text_between_brackets(stripped_line).split('-TO-')
            pending_ids.add(params[1])
            

#for id in pending_ids:
    #print(id)

def robloxifyIDs(string_list):
    prefix = "https://assetdelivery.roblox.com/v2/assetId/"
    modified_list = [prefix + s for s in string_list]
    return modified_list

jsonUrls = robloxifyIDs(pending_ids)

import aiohttp
import asyncio
from tqdm import tqdm

async def download_string(session, url):
    try:
        async with session.get(url) as response:
            return (url.split('/')[-1].strip(), await response.text())
    except Exception as e:
        print(f"Failed to download from {url}: {e}")
        return None

async def download_strings(urls):
    tasks = []
    async with aiohttp.ClientSession() as session:
        for url in urls:
            tasks.append(download_string(session, url))

        results = []
        for f in tqdm(asyncio.as_completed(tasks), total=len(tasks), desc="Downloading"):
            result = await f
            if result is not None:
                results.append(result)
        return results

print("Getting assets metadata...")
metaJsons = asyncio.run(download_strings(jsonUrls))
print("Got assets metadata!")

pendingURLs = set()

import json
for jsonStrTup in metaJsons:
    data = json.loads(jsonStrTup[1])
    try:
        pendingURLs.add((jsonStrTup[0], data['locations'][0]['location']))
    except:
        pass


async def download_file(session, url, folder, fileNamePrefix):
    filename = url.split("/")[-1]
    filepath = os.path.join(folder, fileNamePrefix + '_' + filename)

    try:
        async with session.get(url) as response:
            with open(filepath, "wb") as file:
                while True:
                    chunk = await response.content.read(1024)  # read 1kb at a time
                    if not chunk:
                        break
                    file.write(chunk)
    except Exception as e:
        print(f"Failed to download {url}: {e}")
        
async def download_files(nameUrlsTups, folder):
    if not os.path.exists(folder):
        os.makedirs(folder)

    tasks = []
    async with aiohttp.ClientSession() as session:
        for nameUrlTup in nameUrlsTups:
            tasks.append(download_file(session, nameUrlTup[1], folder, nameUrlTup[0]))

        for f in tqdm(asyncio.as_completed(tasks), total=len(tasks), desc="Downloading"):
            await f
            #f.set_description(f"{f.done()/len(tasks)*100:.2f}% completed")

print("Getting Assets via Metadata!")
os.makedirs('subfolder\\assets', exist_ok=True)
asyncio.run(download_files(pendingURLs, "subfolder\\assets"))
print("all done!")


import shutil

def change_file_type(folder_path, old_extension, new_extension):
    # Iterate over files in the folder
    for filename in os.listdir(folder_path):
        file_path = os.path.join(folder_path, filename)
        
        # Check if the file is a regular file
        if os.path.isfile(file_path):
            # Read the first few bytes of the file
            with open(file_path, 'rb') as file:
                header = file.read(4)

            # Check if the file header matches the expected header
            if header == old_extension:
                # Generate the new file path with the desired extension
                new_file_path = os.path.splitext(file_path)[0] + new_extension

                # Rename the file with the new extension
                shutil.move(file_path, new_file_path)
                print(f"File '{filename}' has been renamed to '{os.path.basename(new_file_path)}'.")

extensions = {
    (b'\xFF\xD8\xFF\xE0', ".jpg"),
    (b'\xFF\xD8\xFF\xE1', ".jpg"),
    (b'\x89PNG', ".png"),
    (b'GIF87a', ".gif"),
    (b'GIF89a', ".gif"),
    (b'BM', ".bmp"),
    (b'II*\x00', ".tiff"),
    (b'MM\x00*', ".tiff"),
    (b'RIFF', ".webp")
}

print("processing image file types")
for ext in extensions:
    change_file_type("subfolder\\assets", ext[0], ext[1])

def roblox_mesh100_to_obj(input_file, output_file):
    with open(input_file, 'r') as f:
        try:
            lines = f.readlines()
        except:
            return False

    # Check mesh version
    version = lines[0].strip()
    if version != 'version 1.00':
        return False

    num_faces = int(lines[1].strip())
    data = lines[2].strip()

    vertices = re.findall(r'\[([^]]*)\]', data)
    if len(vertices) != num_faces * 9:
        return False

    # Parse vertices and write to obj file
    with open(output_file, 'w') as f:
        for i in range(0, len(vertices), 9):
            # Indices for vertices, texture coordinates, and normal vectors in the face
            indices = []

            # Parse position, normal, and UV for each face
            for j in range(3):
                pos = [float(x) / 2 for x in vertices[i + j * 3].split(',')]  # scale down by 0.5
                norm = list(map(float, vertices[i + j * 3 + 1].split(',')))
                uv = list(map(float, vertices[i + j * 3 + 2].split(',')))
                #uv[1] = 1.0 - uv[1]  # flip V coordinate

                # Write to obj file
                f.write('v {} {} {}\n'.format(*pos))
                f.write('vt {} {} {}\n'.format(*uv))
                f.write('vn {} {} {}\n'.format(*norm))

                # Add index to indices list
                index = i//9*3 + j + 1  # Each face consists of 3 vertices
                indices.append(index)

            # Write face
            f.write('f {}/{}/{} {}/{}/{} {}/{}/{}\n'.format(indices[0], indices[0], indices[0],
                                                            indices[1], indices[1], indices[1],
                                                            indices[2], indices[2], indices[2]))
    return True
def roblox_mesh_to_obj_101(file_path, output_file):
    with open(file_path, 'r') as f:
        try:
            lines = f.readlines()
        except:
            return False
        
        version = lines[0].strip()
        num_faces = int(lines[1].strip())
        data = lines[2].strip()
        
        if version != 'version 1.01':
            #print("This function is only compatible with version 1.01 meshes.")
            return False
        
        regex = r'\[([^]]+)\]'
        matches = re.findall(regex, data)
        
        vertices = []
        faces = []
        
        for i in range(num_faces):
            for j in range(3):  # Each face has 3 vertices
                pos = list(map(float, matches[i*9 + j*3].split(',')))
                # For version 1.01, we don't need to scale the position vector
                vertices.append(pos)
                
                tex = list(map(float, matches[i*9 + j*3 + 2].split(',')))
                # The V coordinate is upside down, so we fix it
                tex[1] = 1.0 - tex[1]
                
            # OBJ files use 1-indexed vertices
            faces.append([i*3 + 1, i*3 + 2, i*3 + 3])
        
        with open(output_file, 'w') as out:
            for v in vertices:
                out.write(f'v {v[0]} {v[1]} {v[2]}\n')
            for f in faces:
                out.write(f'f {f[0]} {f[1]} {f[2]}\n')
    return True

# Iterate over files in the folder
for filename in os.listdir("subfolder\\assets"):
    file_path = os.path.join("subfolder\\assets", filename)


    # Check if the file is a regular file
    if os.path.isfile(file_path):
        # only doing 100 & 101 because the game I want is old (Adventure forward star savior 1 my beloved)
        if (roblox_mesh100_to_obj(file_path, file_path + ".obj")):
            print("File \'" + filename + "\' converted to \'" + filename + ".obj\'.")
            os.remove(file_path)
            continue
        elif (roblox_mesh_to_obj_101(file_path, file_path + ".obj")):
            print("File \'" + filename + "\' converted to \'" + filename + ".obj\'.")
            os.remove(file_path)
            continue

input("Press enter to end script")