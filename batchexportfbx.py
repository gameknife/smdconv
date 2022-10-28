# ##### BEGIN GPL LICENSE BLOCK #####
#
#  This program is free software; you can redistribute it and/or
#  modify it under the terms of the GNU General Public License
#  as published by the Free Software Foundation; either version 2
#  of the License, or (at your option) any later version.
#
#  This program is distributed in the hope that it will be useful,
#  but WITHOUT ANY WARRANTY; without even the implied warranty of
#  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
#  GNU General Public License for more details.
#
#  You should have received a copy of the GNU General Public License
#  along with this program; if not, write to the Free Software Foundation,
#  Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
#
# ##### END GPL LICENSE BLOCK #####
# <pep8 compliant>

bl_info = {
    "name": "Batch export FBX files",
    "author": "brockmann",
    "version": (0, 1, 0),
    "blender": (2, 80, 0),
    "location": "File > Import-Export",
    "description": "Batch Export Objects in Selection to FBX",
    "warning": "",
    "wiki_url": "",
    "tracker_url": "",
    "category": "Import-Export"}


import bpy
import os

from mathutils import Matrix, Vector, Euler, Quaternion
from bpy_extras.io_utils import ExportHelper

from bpy.props import (BoolProperty,
                       FloatProperty,
                       StringProperty,
                       EnumProperty,
                       CollectionProperty
                       )


class Batch_FBX_Export(bpy.types.Operator, ExportHelper):
    """Batch export objects to fbx files"""
    bl_idname = "export_scene.batch_fbx"
    bl_label = "Batch export FBX"
    bl_options = {'PRESET', 'UNDO'}

    # ExportHelper mixin class uses this
    filename_ext = ".fbx"

    filter_glob = StringProperty(
            default="*.fbx",
            options={'HIDDEN'},
            )

    # List of operator properties, the attributes will be assigned
    # to the class instance from the operator setting before calling.

    # context group
    use_selection_setting: BoolProperty(
            name="Selection Only",
            description="Export selected objects only",
            default=True,
            )

    use_mesh_modifiers_setting: BoolProperty(
            name="Apply Modifiers",
            description="Apply modifiers (preview resolution)",
            default=True,
            )
    axis_forward_setting: EnumProperty(
            name="Forward",
            items=(('X', "X Forward", ""),
                   ('Y', "Y Forward", ""),
                   ('Z', "Z Forward", ""),
                   ('-X', "-X Forward", ""),
                   ('-Y', "-Y Forward", ""),
                   ('-Z', "-Z Forward", ""),
                   ),
            default='-Z',
            )
    axis_up_setting: EnumProperty(
            name="Up",
            items=(('X', "X Up", ""),
                   ('Y', "Y Up", ""),
                   ('Z', "Z Up", ""),
                   ('-X', "-X Up", ""),
                   ('-Y', "-Y Up", ""),
                   ('-Z', "-Z Up", ""),
                   ),
            default='Y',
            )
    global_scale_setting: FloatProperty(
            name="Scale",
            min=0.01, max=1000.0,
            default=1.0,
            )

    def execute(self, context):                

        # get the folder
        folder_path = os.path.dirname(self.filepath)

        # get objects selected in the viewport
        viewport_selection = context.selected_objects

        # get export objects
        obj_export_list = viewport_selection
        if self.use_selection_setting == False:
            obj_export_list = [i for i in context.scene.objects]

        # deselect all objects
        bpy.ops.object.select_all(action='DESELECT')

        for item in obj_export_list:
            item.select_set(True)
            item.location = Vector([0,0,0])
            item.rotation_quaternion = Vector([1,0,0,0])
            if item.type == 'MESH':
                file_path = os.path.join(folder_path, "{}.fbx".format(item.name))

                # FBX settings
                bpy.ops.export_scene.fbx(
                        filepath=file_path, 
                        use_selection=self.use_selection_setting, 
                        use_active_collection=False, 
                        global_scale=self.global_scale_setting, 
                        apply_unit_scale=True, 
                        apply_scale_options='FBX_SCALE_NONE', 
                        bake_space_transform=False, 
                        object_types={'EMPTY', 'CAMERA', 'LIGHT', 'ARMATURE', 'MESH', 'OTHER'}, 
                        use_mesh_modifiers=self.use_mesh_modifiers_setting, 
                        use_mesh_modifiers_render=True, 
                        mesh_smooth_type='OFF', 
                        use_subsurf=False, 
                        use_mesh_edges=False, 
                        use_tspace=False, 
                        use_custom_props=False, 
                        add_leaf_bones=True, primary_bone_axis='Y', 
                        secondary_bone_axis='X', 
                        use_armature_deform_only=False, 
                        armature_nodetype='NULL', 
                        bake_anim=True, 
                        bake_anim_use_all_bones=True, 
                        bake_anim_use_nla_strips=True, 
                        bake_anim_use_all_actions=True, 
                        bake_anim_force_startend_keying=True, 
                        bake_anim_step=1, 
                        bake_anim_simplify_factor=1, 
                        path_mode='AUTO', 
                        embed_textures=False, 
                        batch_mode='OFF', 
                        use_batch_own_dir=True, 
                        use_metadata=True, 
                        axis_forward=self.axis_forward_setting, 
                        axis_up=self.axis_up_setting
                        )

            item.select_set(False)

        # restore viewport selection
        for ob in viewport_selection:
            ob.select_set(True)

        return {'FINISHED'}


# Only needed if you want to add into a dynamic menu
def menu_func_import(self, context):
    self.layout.operator(Batch_FBX_Export.bl_idname, text="FBX Batch Export (.fbx)")


def register():
    bpy.utils.register_class(Batch_FBX_Export)
    bpy.types.TOPBAR_MT_file_export.append(menu_func_import)


def unregister():
    bpy.utils.unregister_class(Batch_FBX_Export)
    bpy.types.TOPBAR_MT_file_export.remove(menu_func_import)


if __name__ == "__main__":
    register()

    # test call
    #bpy.ops.export_scene.batch_fbx('INVOKE_DEFAULT')