import sqlite3
import numpy as np
import pandas as pd
from sentence_transformers import SentenceTransformer
from sklearn.neighbors import NearestNeighbors

def load_assets_from_file(file_path):
    """Load asset metadata from an Excel or CSV file."""
    try:
        if file_path.endswith(".csv"):
            df = pd.read_csv(file_path)
        else:
            df = pd.read_excel(file_path)

        required_columns = {"name", "description", "type", "path"}
        if not required_columns.issubset(set(df.columns)):
            raise ValueError(f"Spreadsheet must contain columns: {required_columns}")

        assets = []
        for _, row in df.iterrows():
            assets.append({
                "name": str(row["name"]),
                "description": str(row["description"]),
                "type": str(row["type"]),
                "path": str(row["path"])
            })
        return assets

    except Exception as e:
        print(f"Error loading file '{file_path}': {e}")
        return []


class AssetSearchEngine:
    def __init__(self, db_path='assets.db'):
        self.db_path = db_path
        self.model = SentenceTransformer("all-MiniLM-L6-v2")
        self.nn_model = None
        self.embeddings = []
        self.asset_ids = []

    def _connect_db(self):
        return sqlite3.connect(self.db_path)

    def index_sample_assets(self, assets, sample_count):
        """Create and populate the SQLite asset table, then build vector index."""
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
        n_neighbors = min(n_samples, sample_count)
        self.nn_model = NearestNeighbors(n_neighbors=n_neighbors, metric='cosine')
        self.nn_model.fit(np.array(self.embeddings))

    def search(self, query, asset_type=None):
        """Return matching asset paths based on semantic similarity."""
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
                if asset_type is None or type_.lower() == asset_type.lower():
                    results.append(path)

        conn.close()
        return results


# 🔍 Example Usage
if __name__ == "__main__":
    asset_file = "AssetsDatabase.xlsx"  # Can also be .csv
    assets = load_assets_from_file(asset_file)

    if not assets:
        print("No assets to index. Exiting.")
    else:
        engine = AssetSearchEngine()
        engine.index_sample_assets(assets, sample_count=5)
