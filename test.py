import os
from pathlib import Path
import argparse

def generate_tree(directory_path, prefix="", ignore_patterns=None, ignore_extensions=None):
    """
    Generate a tree structure of the given directory.
    
    Args:
        directory_path (str): Path to the directory
        prefix (str): Prefix for the current item (used for indentation)
        ignore_patterns (list): List of directories/files to ignore
        ignore_extensions (list): List of file extensions to ignore (e.g., ['.pyc', '.log'])
    """
    if ignore_patterns is None:
        ignore_patterns = ['.git', 'node_modules', '__pycache__', '.pytest_cache', '.vscode']
    if ignore_extensions is None:
        ignore_extensions = []
        
    # Get the directory contents
    path = Path(directory_path)
    
    # Get directories and files separately and sort them
    try:
        items = list(path.iterdir())
        directories = sorted([item for item in items if item.is_dir()])
        files = sorted([item for item in items if item.is_file()])
    except PermissionError:
        return f"{prefix}├── [Permission Denied]\n"
    
    tree = ""
    
    # Process directories
    for i, directory in enumerate(directories):
        # Skip if directory matches any ignore pattern
        if (directory.name.startswith('.') or 
            any(pattern.lower() in str(directory).lower() for pattern in ignore_patterns)):
            continue
            
        is_last_dir = (i == len(directories) - 1 and len(files) == 0)
        connector = "└── " if is_last_dir else "├── "
        
        tree += f"{prefix}{connector}{directory.name}/\n"
        extension = "    " if is_last_dir else "│   "
        tree += generate_tree(directory, prefix + extension, ignore_patterns, ignore_extensions)
    
    # Process files
    for i, file in enumerate(files):
        # Skip if file matches any ignore pattern or extension
        if (file.name.startswith('.') or 
            any(pattern.lower() in file.name.lower() for pattern in ignore_patterns) or
            any(file.name.lower().endswith(ext.lower()) for ext in ignore_extensions)):
            continue
            
        is_last = (i == len(files) - 1)
        connector = "└── " if is_last else "├── "
        tree += f"{prefix}{connector}{file.name}\n"
    
    return tree

def main():
    parser = argparse.ArgumentParser(description='Generate a tree structure of a directory')
    parser.add_argument('path', nargs='?', default='.', help='Path to the directory (default: current directory)')
    parser.add_argument('--ignore', nargs='*', help='Additional patterns to ignore (folders or files)')
    parser.add_argument('--ignore-ext', nargs='*', help='File extensions to ignore (e.g., .pyc .log)')
    
    args = parser.parse_args()
    
    # Default ignore patterns
    ignore_patterns = [ 'node_modules', '__pycache__', '.pytest_cache','Dockerfile','docker-compose.yml','.dockerignore', '.vscode']
    
    # Add user-provided patterns
    if args.ignore:
        ignore_patterns.extend(args.ignore)
    
    # Process ignore extensions
    ignore_extensions = []
    if args.ignore_ext:
        ignore_extensions = [ext if ext.startswith('.') else f'.{ext}' for ext in args.ignore_ext]
    
    # Get the absolute path
    directory_path = os.path.abspath(args.path)
    
    # Print the root directory name
    print(f"\nProject Structure for: {directory_path}")
    print(f"Ignoring: {', '.join(ignore_patterns)}")
    if ignore_extensions:
        print(f"Ignoring extensions: {', '.join(ignore_extensions)}")
    print()
    
    # Generate and print the tree
    tree = generate_tree(directory_path, ignore_patterns=ignore_patterns, ignore_extensions=ignore_extensions)
    print(tree)

if __name__ == "__main__":
    main()