const DI = require("../utils/discord_interface");
const {Message} = require("../utils/message");
const {Interaction} = require("../utils/interaction");
const {User} = require("../utils/user");
const CONFIG = require("../config");
const {CommandDispatcher} = require("./command_dispatcher");
const {ButtonComponent} = require("discord.js");


class EventManager {
    constructor(client) {
        this._bound_modules = []
        this._command_modules = {}
        this._command_manager = new CommandDispatcher(client)
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
                if (!this._interactions[interaction.message.interaction.id]) {
                    new Interaction(this._command_manager.find(interaction.commandName), interaction).skip()
                    return
                }
                const inter = this._interactions[interaction.message.interaction.id]
                if (inter) {
                    let removed = []
                    for (const callback in inter) {
                        if (inter[callback](
                            interaction.customId,
                            interaction.message.interaction.id,
                            new Message(interaction.message)) === false)
                            removed.push(callback)
                    }
                    for (let i = removed.length; i >= 0; i--)
                        this._interactions[interaction.message.interaction.id].splice(removed[i], 1)
                    if (this._interactions[interaction.message.interaction.id].length === 0)
                        delete this._interactions[interaction.message.interaction.id]
                }
                new Interaction(this._command_manager.find(interaction.commandName), interaction).skip()
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
            if (command_interaction.permissions() && !command.has_permission(command_interaction.permissions())) {
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
                        .catch(err => console.error(`Failed to call 'server_message()' on module ${module.name} :\n${err}`))
                })()
    }

    _dm_message(message) {
        for (const module of this._bound_modules)
            if (module.dm_message)
                (async () => {
                    module.dm_message(message)
                        .catch(err => console.error(`Failed to call 'dm_message()' on module ${module.name} :\n${err}`))
                })()
    }

    _server_message_updated(old_message, new_message) {
        for (const module of this._bound_modules)
            if (module.server_message_updated)
                (async () => {
                    module.server_message_updated(old_message, new_message)
                        .catch(err => console.error(`Failed to call 'server_message_updated()' on module ${module.name} :\n${err}`))
                })()
    }

    _server_message_delete(message) {
        for (const module of this._bound_modules)
            if (module.server_message_delete)
                (async () => {
                    module.server_message_delete(message)
                        .catch(err => console.error(`Failed to call 'server_message_delete()' on module ${module.name} :\n${err}`))
                })()
    }

    get_commands(permissions) {
        return this._command_manager ? this._command_manager.get_commands(permissions) : null
    }

    watch_interaction(interaction_id, callback) {
        if (!this._interactions[interaction_id])
            this._interactions[interaction_id] = [callback]
        else
            this._interactions[interaction_id].push(callback)
    }
}

module.exports = {EventManager}