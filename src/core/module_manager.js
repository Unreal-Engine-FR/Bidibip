const fs = require('fs')
const path = require('path')
const {EventManager} = require("./event_manager");

class ModuleManager {
    constructor() {
        this._module_list = {}
    }

    /**
     * Initialize module manager with given discord client (can be used to reload everything)
     * @param discord_client
     */
    init(discord_client) {
        // Unload existing modules
        this.unload_modules()

        // Init modules with new client
        this._client = discord_client
        this._event_manager = new EventManager(this._client)
        this.load_all_modules()
    }

    load_all_modules() {
        try {
            const names = fs.readdirSync(path.join(__dirname, '../modules'), {withFileTypes: true})
                .filter(dirent => dirent.isDirectory())
                .map(dirent => dirent.name)

            for (const module of names) {
                this.load_module(module)
            }
        } catch (err) {
            console.fatal("failed to load modules : " + err)
        }
    }

    /**
     * Try to load a module by name (module will be automatically started if enabled is not set to false by default)
     * @param module_name {string}
     */
    load_module(module_name) {
        import('../modules/' + module_name + '/module.js')
            .then(module => {
                if (this._module_list[module_name])
                    console.warning(`Module '${module_name}' is already loaded`)
                else {
                    let instance = new module.Module({
                        client: this._client,
                    })
                    instance.name = module_name
                    this._module_list[module_name] = instance

                    // Start module if not specified to be stopped by default
                    if (instance.enabled !== false) {
                        instance.enabled = false
                        this.start(module_name)
                    }
                    console.validate(`Successfully loaded module '${module_name}'`)
                }
            })
            .catch(error => {
                console.fatal(`Failed to load module '${module_name}' : ${error}`)
            })
    }

    /**
     * Try to unload module by name (module will be stopped first if it is enabled)
     * @param module_name {string}
     */
    unload_module(module_name) {
        if (!this._module_list[module_name]) {
            return console.error(`Module '${module_name}' does not exists`)
        }

        // Disable module
        if (this._module_list[module_name].enabled === true)
            this.stop(module_name)

        delete this._module_list[module_name]

        console.validate(`Successfully unloaded module '${module_name}'`)
    }

    /**
     * Unload all loaded modules
     */
    unload_modules() {
        for (const module of this.loaded_modules()) {
            this.unload_module(module)
        }
    }

    /**
     * Get a list of loaded module names
     * @returns {string[]}
     */
    loaded_modules() {
        let modules = []
        for (const [key, _] of Object.entries(this._module_list))
            modules.push(key)
        return modules
    }

    /**
     * Start a module by name (return if already started)
     * @param module_name {string}
     */
    start(module_name) {
        if (!this._module_list[module_name]) {
            console.error(`There is no module called '${module_name}'`)
            return
        }

        const module = this._module_list[module_name]
        if (module.enabled === false) {
            module.enabled = true
            this._event_manager.bind(module)
            if (module.start)
                module.start()
            console.validate(`Successfully enabled module '${module_name}'`)
        } else
            console.warning(`Module '${module_name}' is already enabled`)
    }

    /**
     * Stop a module by name (return if already stopped)
     * @param module_name {string}
     */
    stop(module_name) {
        if (!this._module_list[module_name]) {
            console.error(`There is no module called '${module_name}'`)
            return
        }

        const module = this._module_list[module_name]
        if (module.enabled === true) {
            if (module.stop)
                module.stop()
            this._event_manager.unbind(module)
            module.enabled = false
            console.validate(`Successfully disabled module '${module_name}'`)
        } else
            console.warning(`Module '${module_name}' is already disabled`)
    }

    /**
     * Get event manager
     * @return {EventManager}
     */
    event_manager() {
        return this._event_manager
    }

    /**
     * Get state about all modules
     * @return {*[]}
     */
    all_modules_info() {
        const names = fs.readdirSync(path.join(__dirname, '../modules'), {withFileTypes: true})
            .filter(dirent => dirent.isDirectory())
            .map(dirent => dirent.name)

        let modules = []
        for (const module_name of names) {
            modules.push({
                name: module_name,
                loaded: !!this._module_list[module_name],
                enabled: this._module_list[module_name] && this._module_list[module_name].enabled
            })
        }
        return modules
    }
}

/**
 * Module manager global singleton
 * @type {ModuleManager}
 */
let MODULE_MANAGER = null

function get() {
    if (MODULE_MANAGER === null)
        MODULE_MANAGER = new ModuleManager()
    return MODULE_MANAGER
}

module.exports = {get}