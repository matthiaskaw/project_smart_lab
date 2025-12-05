import sqlite3
import json

# Connect to database
conn = sqlite3.connect('smartlab.db')
cursor = conn.cursor()

# First, list all tables
print("=== TABLES IN DATABASE ===")
cursor.execute("SELECT name FROM sqlite_master WHERE type='table'")
tables = cursor.fetchall()
for table in tables:
    print(table[0])

print("\n=== DATASETS ===")
cursor.execute("SELECT Id, Name, DataSource, RawDataJson FROM Datasets LIMIT 5")
datasets = cursor.fetchall()

for dataset in datasets:
    dataset_id, name, data_source, raw_json = dataset
    print(f"\nDataset: {name} (ID: {dataset_id})")
    print(f"DataSource: {data_source}")

    if raw_json:
        try:
            raw_data = json.loads(raw_json)
            print(f"RawDataJson lines: {len(raw_data)}")
            if len(raw_data) > 0:
                print(f"First line: {raw_data[0]}")
                if len(raw_data) > 1:
                    print(f"Second line: {raw_data[1]}")
        except:
            print(f"RawDataJson: {raw_json[:100]}...")
    else:
        print("RawDataJson: NULL or empty")

    # Check if DataPoints exist for this dataset
    cursor.execute("SELECT COUNT(*) FROM DataPoints WHERE DatasetId = ?", (dataset_id,))
    point_count = cursor.fetchone()[0]
    print(f"DataPoints in table: {point_count}")

conn.close()
