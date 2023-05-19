// MODULE UTILITIES
const {CommandInfo, Message, Embed} = require("../../discord_interface");
const MODULE_MANAGER = require('../../module_manager').get()
const CONFIG = require('../../config').get()
const LOGGER = require('../../logger').get()

class Module {
    /**
     * @param create_infos contains module infos => {
     *      client: discord_client
     * }
     */
    constructor(create_infos) {
        this.enabled = true // default value is true

        this.client = create_infos.client

        // Command declaration
        this.commands = [
            new CommandInfo('modules_infos', 'Informations sur les modules')
                .set_admin_only(),
            new CommandInfo('set_module_enabled', 'Active ou desactive un module')
                .add_text_option('nom', 'nom du module')
                .add_bool_option('enabled', 'Nouvel etat du module')
                .set_admin_only(),
            new CommandInfo('load_module', 'Charge un module')
                .add_text_option('nom', 'nom du module')
                .set_admin_only(),
            new CommandInfo('unload_module', 'Decharge un module')
                .add_text_option('nom', 'nom du module')
                .set_admin_only(),
            new CommandInfo('update', 'Vérifie les mises à jour et les effectue le cas échéant')
                .set_admin_only(),
            new CommandInfo('restart', 'Fait redémarrer le bot')
                .set_admin_only()
        ]

        const update_activity = () => {
            create_infos.client.get_user_count().then(members => {
                create_infos.client.set_activity(`${members} Utilisateurs`)
            })
                .catch(err => console.fatal(`failed to set activity : ${err}`))
        }
        update_activity()
        setInterval(update_activity, 3600000)

        LOGGER.bind((level, message) => {
            if (level === 'E' || level === 'F') {
                create_infos.client.say(new Message()
                    .set_text('A BOBO ' + CONFIG.SERVICE_ROLE + ' !!! :(')
                    .set_channel(CONFIG.LOG_CHANNEL_ID)
                    .add_embed(new Embed()
                        .set_title(level === 'E' ? 'Error' : 'Fatal')
                        .set_description(message)))
            }
        })
    }

    // When module is started
    start() {
    }

    // When module is stopped
    stop() {
    }

    server_command(command) {
        if (command.match('modules_infos')) {
            const embed = new Embed()
                .set_title('modules')
                .set_description('liste des modules disponibles')

            for (const module of MODULE_MANAGER.all_modules_info()) {
                embed.add_field(module.name, `chargé : ${module.loaded ? ':white_check_mark:' : ':x:'}\tactivé : ${module.enabled ? ':white_check_mark:' : ':x:'}`)
            }

            command.reply(
                new Message()
                    .add_embed(embed)
                    .set_client_only()
            )
        }
        if (command.match('set_module_enabled')) {
            if (command.option_value('enabled') === true)
                MODULE_MANAGER.start(command.option_value('nom'))
            else
                MODULE_MANAGER.stop(command.option_value('nom'))
            command.skip()
        }
        if (command.match('load_module')) {
            MODULE_MANAGER.load_module(command.option_value('nom'))
            command.skip()
        }
        if (command.match('unload_module')) {
            MODULE_MANAGER.unload_module(command.option_value('nom'))
            command.skip()
        }
        if (command.match('update')) {
            this.client.updater.compareVersions()
                .then(res => {
                    if (res.upToDate) {
                        command.reply(new Message()
                            .set_text(`Je suis à jour ! (version ${res.currentVersion})`)
                            .set_client_only())
                    } else {
                        command.reply(new Message()
                            .set_text(`Mise à jour disponible, Redémarage en cours ! (${res.currentVersion} => ${res.remoteVersion})`)
                            .set_client_only())
                            .then(_ => {
                                console.warning("restarting for update...")
                                process.exit(0)
                            })
                    }
                })
                .catch(err => {
                    command.reply(new Message().set_text(`Impossible de vérifier les mises à jour : ${err}`).set_client_only())
                })
        }
        if (command.match('restart')) {
            command.reply(new Message()
                .set_text(`Redémarrage en cours...`)
                .set_client_only())
                .then(_ => {
                    console.warning("restarting...")
                    process.exit(0)
                })
        }
    }

    // On received server messages=
    server_message(message) {
    }

    // When interaction button is clicked (interaction should have been bound before)
    receive_interaction(value, id, message) {
    }

    // On update message on server
    server_message_updated(old_message, new_message) {
    }

    // On delete message on server
    server_message_delete(message) {
    }
}

module.exports = {Module}