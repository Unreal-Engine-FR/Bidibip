const fs = require('fs')
const path = require('path')
const DI = require('./discord_interface')
const {Message} = require('./utils/message')
const {Interaction} = require("./utils/interaction");
const CONFIG = require("./config");
const {User} = require("./utils/user");

class CommandManager {
    constructor() {
        this.modules = {}
        this.commands = {}
    }

    add(module) {
        if (this.modules[module.name])
            return // already added

        if (!module.commands)
            return // doesn't implement old

        this.modules[module.name] = {module: module, command: module.commands}

        for (const command of module.commands) {
            if (this.commands[command.name])
                this.commands[command.name].modules.push(module)
            else
                this.commands[command.name] = {
                    command: command,
                    modules: [module]
                }
        }

        this._refresh_commands()
    }

    remove(module) {
        if (!this.modules[module.name])
            return // already removed

        delete this.modules[module.name]

        for (const [key, command] of Object.entries(this.commands)) {
            if (command.modules.indexOf(module) !== -1)
                command.modules.splice(command.modules.indexOf(module), 1)
            if (command.modules.length === 0)
                delete this.commands[key]
        }

        this._refresh_commands()
    }

    _refresh_commands() {
        if (this.refresh_timer)
            clearInterval(this.refresh_timer)

        const cm = this
        this.refresh_timer = setTimeout(() => {
            DI.get().set_slash_commands(cm.all_commands())
        }, 100)
    }

    all_commands() {
        let commands = []
        for (const [_, value] of Object.entries(this.commands))
            commands.push(value.command)
        return commands
    }

    find(command_name) {
        const command = this.commands[command_name]
        return command ? command.command : null
    }

    execute_command(command) {
        const found_command = this.commands[command.source_command().name]
        if (found_command)
            for (const module of found_command.modules)
                if (module.server_interaction)
                    (async () => {
                        module.server_interaction(command)
                            .catch(err => console.error(`Failed to call 'server_interaction()' on module ${module.name} : ${err}`))
                    })()

    }

    get_commands(permissions) {
        let commands = []
        for (const [_, command] of Object.entries(this.commands))
            if (command.command.has_permission(permissions))
                commands.push(command.command)
        return commands
    }
}

class EventManager {
    constructor(client) {
        this._bound_modules = []
        this._command_modules = {}
        this._command_manager = new CommandManager(client)
        this._interactions = {}

        DI.get().on_message_delete = msg => {
            const message = new Message(msg)
            if (!message.is_dm())
                this._server_message_delete(message)
        }
        DI.get().on_message_update = (old_message, new_message) => {
            const old = new Message(old_message)
            if (!old.is_dm())
                this._server_message_updated(old, new Message(new_message))
        }

        DI.get().on_message = msg => {
            const message = new Message(msg)
            if (message.is_dm())
                this._dm_message(message)
            else
                this._server_message(message)
        }

        DI.get().on_interaction = interaction => {
            if (interaction.isButton()) {
                const inter = this._interactions[interaction.message.interaction.id]
                if (inter) {
                    for (const module of inter) {
                        if (module.receive_interaction) {
                            module.receive_interaction(
                                interaction.customId,
                                interaction.message.interaction.id,
                                new Message(interaction.message)
                            )
                        }
                    }
                }
                new Interaction(null, interaction).skip()
                    .catch(err => console.fatal(`failed to skip interaction : ${err}`))
                return
            }

            let options = ''
            for (const option of interaction.options._hoistedOptions) {
                options += option.name + ': ' + option.value + ', '
            }
            options = options.substring(0, options.length - 2)

            console.info(`User [${interaction.user.username}#${interaction.user.discriminator}] issued {'${interaction.commandName} ${options}'}`)

            const command = this._command_manager.find(interaction.commandName)
            if (command === null) {
                interaction.reply(new Message().set_text("Commande inconnue").set_client_only())
                return
            }

            const command_interaction = new Interaction(command, interaction)
            if (!command.has_permission(command_interaction.permissions())) {
                console.log('a')
                new User(interaction.user).full_name()
                    .then(name => {
                        new Message()
                            .set_channel(CONFIG.get().LOG_CHANNEL_ID)
                            .set_text(`${name} (${'<@' + new User(interaction.user).id() + '>'}) a essayé d'exécuter la commande '${command.name}' sans en avoir la permission ${CONFIG.get().SERVICE_ROLE} !`)
                            .send()
                            .catch(err => console.fatal(`failed to notify usage error : ${err}`))
                        console.warning(`@${name} a essayé d\'exécuter la commande \'${command.name}\' sans en avoir la permission !`)
                    })
                    .catch(err => {
                        console.fatal(`failed to notify usage error : ${err}`)
                    })
                console.warning('Invalid permission usage !')
                command_interaction.skip()
                    .catch(err => console.fatal(`failed to skip interaction : ${err}`))
            } else
                this._command_manager.execute_command(command_interaction)
        }
    }

    bind(module) {
        const index = this._bound_modules.indexOf(module)
        if (index !== -1)
            return // already bound

        // Register old
        if (module.commands)
            for (const command of module.commands) {
                if (!this._command_modules[command.name])
                    this._command_modules[command.name] = [module]
                else
                    this._command_modules[command.name].push(module)
            }

        this._command_manager.add(module)
        this._bound_modules.push(module)
    }

    unbind(module) {
        const index = this._bound_modules.indexOf(module)
        if (index === -1)
            return // already unbound

        this._command_manager.remove(module)
        this._bound_modules.splice(this._bound_modules.indexOf(module), 1)

        // Unregister old
        for (const [, value] of Object.entries(this._command_modules))
            if (value.indexOf(module) !== 0)
                value.splice(value.indexOf(module), 1)
    }

    _server_message(message) {
        for (const module of this._bound_modules)
            if (module.server_message)
                (async () => {
                    module.server_message(message)
                        .catch(err => console.error(`Failed to call 'server_message()' on module ${module.name} : ${err}`))
                })()
    }

    _dm_message(message) {
        for (const module of this._bound_modules)
            if (module.dm_message)
                (async () => {
                    module.dm_message(message)
                        .catch(err => console.error(`Failed to call 'dm_message()' on module ${module.name} : ${err}`))
                })()
    }

    _server_message_updated(old_message, new_message) {
        for (const module of this._bound_modules)
            if (module.server_message_updated)
                (async () => {
                    module.server_message_updated(old_message, new_message)
                        .catch(err => console.error(`Failed to call 'server_message_updated()' on module ${module.name} : ${err}`))
                })()
    }

    _server_message_delete(message) {
        for (const module of this._bound_modules)
            if (module.server_message_delete)
                (async () => {
                    module.server_message_delete(message)
                        .catch(err => console.error(`Failed to call 'server_message_delete()' on module ${module.name} : ${err}`))
                })()
    }

    get_commands(permissions) {
        return this._command_manager ? this._command_manager.get_commands(permissions) : null
    }

    watch_interaction(module, interaction_id) {
        if (!this._interactions[interaction_id])
            this._interactions[interaction_id] = [module]
        else if (this._interactions[interaction_id].indexOf(module) === -1)
            this._interactions[interaction_id].push(module)
    }

    release_interaction(module, interaction_id) {
        if (this._interactions[interaction_id] && this._interactions[interaction_id].indexOf(module) !== -1) {
            this._interactions[interaction_id].splice(this._interactions[interaction_id].indexOf(module), 1)
            if (this._interactions[interaction_id].length === 0)
                delete this._interactions[interaction_id]
        }
    }
}

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
            const names = fs.readdirSync(path.join(__dirname, './modules'), {withFileTypes: true})
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
     * @param module_name
     */
    load_module(module_name) {
        import('./modules/' + module_name + '/module.js')
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
     * @param module_name
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
     * @returns {ModuleManager[]}
     */
    loaded_modules() {
        let modules = []
        for (const [key, _] of Object.entries(this._module_list))
            modules.push(key)
        return modules
    }

    /**
     * Start a module by name (return if already started)
     * @param module_name
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
     * @param module_name
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

    event_manager() {
        return this._event_manager
    }

    all_modules_info() {
        const names = fs.readdirSync(path.join(__dirname, './modules'), {withFileTypes: true})
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