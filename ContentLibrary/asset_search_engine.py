
import sqlite3
import numpy as np
from sentence_transformers import SentenceTransformer
from sklearn.neighbors import NearestNeighbors

class AssetSearchEngine:
    def __init__(self, db_path='assets.db'):
        self.db_path = db_path
        self.model = SentenceTransformer("all-MiniLM-L6-v2")
        self.nn_model = None
        self.embeddings = []
        self.asset_ids = []
        self._load_assets_and_build_index()

    def _connect_db(self):
        return sqlite3.connect(self.db_path)

    def _load_assets_and_build_index(self):
        conn = self._connect_db()
        cursor = conn.cursor()
        cursor.execute("SELECT id, name, description FROM assets")
        rows = cursor.fetchall()
        conn.close()

        self.embeddings = []
        self.asset_ids = []
        for row in rows:
            id_, name, description = row
            self.asset_ids.append(id_)
            text = f"{name} {description}"
            embedding = self.model.encode(text)
            self.embeddings.append(embedding)

        if self.embeddings:
            n_samples = len(self.embeddings)
            n_neighbors = min(n_samples, 5)
            self.nn_model = NearestNeighbors(n_neighbors=n_neighbors, metric='cosine')
            self.nn_model.fit(np.array(self.embeddings))

    def index_sample_assets(self, assets, sampleCount):
        conn = self._connect_db()
        cursor = conn.cursor()

        cursor.execute("DROP TABLE IF EXISTS assets")
        cursor.execute("""
            CREATE TABLE assets (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT,
                description TEXT,
                type TEXT,
                path TEXT
            )
        """)
        conn.commit()

        self.embeddings = []
        self.asset_ids = []

        for asset in assets:
            text = f"{asset['name']} {asset['description']}"
            vector = self.model.encode(text)
            self.embeddings.append(vector)
            cursor.execute("INSERT INTO assets (name, description, type, path) VALUES (?, ?, ?, ?)",
                           (asset['name'], asset['description'], asset['type'], asset['path']))
            self.asset_ids.append(cursor.lastrowid)

        conn.commit()
        conn.close()

        n_samples = len(self.embeddings)
        n_neighbors = min(n_samples, sampleCount)
        self.nn_model = NearestNeighbors(n_neighbors=n_neighbors, metric='cosine')
        self.nn_model.fit(np.array(self.embeddings))

    def search(self, query, asset_type=None):
        if not self.nn_model:
            return []

        query_embedding = self.model.encode(query).reshape(1, -1)
        distances, indices = self.nn_model.kneighbors(query_embedding)

        conn = self._connect_db()
        cursor = conn.cursor()

        results = []
        for idx in indices[0]:
            asset_id = self.asset_ids[idx]
            cursor.execute("SELECT path, type FROM assets WHERE id=?", (asset_id,))
            row = cursor.fetchone()
            if row:
                path, type_ = row
                if asset_type is None or type_ == asset_type:
                    results.append(path)

        conn.close()
        return results

# Example Usage
if __name__ == "__main__":
    assets = [
	#tests
        {"name": "Red Dragon Texture", "description": "A detailed texture of a fierce red dragon.", "type": "Texture", "path": "/assets/textures/red_dragon.png"},
        {"name": "Red Dragon Model", "description": "A 3D Model of a fierce red dragon.", "type": "3D Model", "path": "/assets/textures/red_dragon.fbx"},
        {"name": "Forest Background", "description": "A lush green forest background with trees and mist.", "type": "Image", "path": "/assets/backgrounds/forest.jpg"},
        {"name": "Magic Sword Model", "description": "A 3D model of an enchanted sword with glowing runes.", "type": "3D Model", "path": "/assets/models/magic_sword.obj"},
        {"name": "Player Character Prefab", "description": "A complete player character with animations.", "type": "Prefab", "path": "/assets/prefabs/player.prefab"},
        #Default 
        {"name": "default", "description": "default", "type": "default", "path": "Prefabs/Primitives/cube.prefab,Scripts/ObjectInterface.cs"},
	#Primitives
        {"name": "capsule", "description": "primitive capsule", "type": "prefab", "path": "Prefabs/Primitives/capsule.prefab,Scripts/ObjectInterface.cs"},
        {"name": "cube", "description": "primitive cube", "type": "prefab", "path": "Prefabs/Primitives/cube.prefab,Scripts/ObjectInterface.cs"},
        {"name": "cylinder", "description": "primitive cylinder", "type": "Prefab", "path": "Prefabs/Primitives/cylinder.prefab,Scripts/ObjectInterface.cs"},
        {"name": "plane", "description": "primitive plane", "type": "prefab", "path": "Prefabs/Primitives/plane.prefab,Scripts/ObjectInterface.cs"},
        {"name": "quade", "description": "primitive quade", "type": "prefab", "path": "Prefabs/Primitives/quade.prefab,Scripts/ObjectInterface.cs"},
        {"name": "sphere", "description": "primitive sphere", "type": "prefab", "path": "Prefabs/Primitives/sphere.prefab,Scripts/ObjectInterface.cs"},
	#scripts
        {"name": "spin controller", "description": "spin controller", "type": "script", "path": "Scripts/SpinController.cs"},
	#prefabs
	#Package prefabs
	{"name": "swat", "description": "swat player character", "type": "package", "path": "Packages/PlayerSwat/Playerswat.unitypackage,Prefabs/PlayerSwat/PlayerSwat.prefab" },
	{"name": "terrain01", "description": "depression terrain", "type": "package", "path": "Packages/Terrains/terrain01.unitypackage,Prefabs/terrains/terrain01.prefab" },
	{"name": "Millitary Base", "description": "a millitary base island", "type": "package", "path": "Packages/Terrains/MillitaryBaseIsland.unitypackage,Prefabs/Terrains/MillitaryBaseIsland.prefab" },
	{"name": "FisrPersonCamera", "description": "First-person camera", "type": "package", "path": "Packages/Cameras/FirstPersonCamera.unitypackage,Prefabs/Cameras/FirstPersonCamera.prefab" },
	{"name": "ThirdPersonCamera", "description": "Third-person camera", "type": "package", "path": "Packages/Cameras/ThirdPersonCamera.unitypackage,Prefabs/Cameras/ThirdPersonCamera.prefab" },
	{"name": "TopDownCamera", "description": "Top-down camera", "type": "package", "path": "packages/Cameras/TopDownCamera.unitypackage,Prefabs/Cameras/TopDownCamera.prefab" },
	{"name": "ObserveCamera", "description": "Observe camera", "type": "package", "path": "Packages/Cameras/ObserveCamera.unitypackage,Prefabs/Cameras/ObserveCamera.prefab" },
	{"name": "PlainGround", "description": "Plain ground", "type": "package", "path": "Packages/Natual/PlainGround.unitypackage,Prefabs/Natual/PlainGround.prefab" },
	{"name": "Sky", "description": "Sky with cloud, day of time systems", "type": "package", "path": "Packages/Natual/Sky.unitypackage,Prefabs/Natual/SKy/Sky Dome.prefab" },
	{"name": "Sun", "description": "Directional sun light ", "type": "package", "path": "Packages/Natual/Sun.unitypackage,Prefabs/Natual/Sun.prefab" },
	{"name": "WaterPlane", "description": "water, ocean, lake, sea", "type": "package", "path": "Packages/Natual/WaterPlane.unitypackage,Prefabs/Natual/WaterPlane/WaterPlane.prefab" },
	{"name": "Gun_AK", "description": "Gun AK47 rifle", "type": "package", "path": "Packages/Guns/Gun_AK.unitypackage,Prefabs/Guns/Gun_AK/Gun_AK.prefab" },
	{"name": "Gun_G36", "description": "Gun G36 rifle", "type": "package", "path": "Packages/Guns/Gun_G36.unitypackage,Prefabs/Guns/Gun_G36/Gun_G36.prefab" },
	{"name": "Gun_L85", "description": "Gun L85 rifle", "type": "package", "path": "Packages/Guns/Gun_L85.unitypackage,Prefabs/Guns/Gun_L85/Gun_L85.prefab" },
	{"name": "Gun_M4", "description": "Gun M4 rifle", "type": "package", "path": "Packages/Guns/Gun_M4.unitypackage,Prefabs/Guns/Gun_M4/Gun_M4.prefab" },
	{"name": "Gun_MP5", "description": "Gun MP5 rifle", "type": "package", "path": "Packages/Guns/Gun_MP5.unitypackage,Prefabs/Guns/Gun_MP5/Gun_MP5.prefab" },
	{"name": "Gun_Scar", "description": "Gun Scar rifle", "type": "package", "path": "Packages/Guns/Gun_Scar.unitypackage,Prefabs/Guns/Gun_Scar/Gun_Scar.prefab" },
	{"name": "Gun_Type97", "description": "Gun Type97 rifle", "type": "package", "path": "Packages/Guns/Gun_Type97.unitypackage,Prefabs/Guns/Gun_Type97/Gun_Type97.prefab" },
	{"name": "Simple UI", "description": "Simple UI", "type": "package", "path": "Packages/UI/SimpleUI.unitypackage,Prefabs/SimpleUI/SimpleUI.prefab" },
	{"name": "Knight", "description": "Warrior knight horseman", "type": "package", "path": "Packages/Knight/Knight.unitypackage,Prefabs/Knight/Knight.prefab" }
    ]

    engine = AssetSearchEngine()
    engine.index_sample_assets(assets, 3)

    print("Search result:", engine.search("fire dragon skin"))
    print("Search 3D:", engine.search("enchanted sword", asset_type="3D Model"))
    print("Search Image:", engine.search("misty forest", asset_type="Image"))
    print("Search:", engine.search("dragon character with sword"))
    print("Search:", engine.search("a beatiful cube", asset_type="prefab"))
    print("Search:", engine.search("a brave swat soldier", asset_type="package"))

