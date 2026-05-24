import { Injectable, signal } from '@angular/core';

export type AdminLang = 'en' | 'ru' | 'kk';

const T: Record<AdminLang, Record<string, string>> = {
  en: {
    // Navigation
    'nav.dashboard': 'Dashboard', 'nav.orders': 'Live Orders', 'nav.menu': 'Menu Management',
    'nav.locations': 'Locations', 'nav.analytics': 'Analytics', 'nav.promotions': 'Promotions',
    'nav.customers': 'Customers', 'nav.workers': 'Workers', 'nav.auditLog': 'Audit Log',
    'nav.signOut': 'Sign out', 'nav.console': 'Management Console',
    'auditLog.title': 'Audit Log', 'auditLog.timestamp': 'Timestamp', 'auditLog.admin': 'Admin',
    'auditLog.action': 'Action', 'auditLog.entity': 'Entity', 'auditLog.details': 'Details',
    'auditLog.noRecords': 'No audit records found.', 'auditLog.loadMore': 'Load more',
    'auditLog.filterEntity': 'Filter by entity type…', 'auditLog.all': 'All',

    // Common actions
    'action.add': 'Add', 'action.edit': 'Edit', 'action.delete': 'Delete', 'action.save': 'Save',
    'action.cancel': 'Cancel', 'action.close': 'Close', 'action.confirm': 'Confirm',
    'action.search': 'Search…', 'action.refresh': 'Refresh', 'action.retry': 'Retry',

    // Common labels
    'label.name': 'Name', 'label.description': 'Description', 'label.price': 'Price',
    'label.category': 'Category', 'label.available': 'Available', 'label.active': 'Active',
    'label.imageUrl': 'Image URL', 'label.yes': 'Yes', 'label.no': 'No', 'label.free': 'Free',
    'label.loading': 'Loading…', 'label.group': 'Group', 'label.categories': 'Categories',
    'label.phone': 'Phone', 'label.address': 'Address', 'label.workingHours': 'Working Hours',
    'label.saving': 'Saving…', 'label.inactive': 'Inactive', 'label.status': 'Status',
    'label.role': 'Role', 'label.created': 'Created', 'label.actions': 'Actions',

    // Menu management
    'menu.title': 'Menu Management', 'menu.categories': 'Categories', 'menu.allItems': 'All Items',
    'menu.addCategory': '+ Add Category', 'menu.items': 'Items', 'menu.toppings': 'Toppings',
    'menu.addItem': '+ Add Item', 'menu.addTopping': '+ Add Topping',
    'menu.addMenuItemTitle': 'Add Menu Item', 'menu.editMenuItemTitle': 'Edit Menu Item',
    'menu.addCategoryTitle': 'Add Category', 'menu.editCategoryTitle': 'Edit Category',
    'menu.addToppingTitle': 'Add Topping', 'menu.editToppingTitle': 'Edit Topping',
    'menu.noItems': 'No items in this category', 'menu.noToppings': 'No toppings yet',
    'menu.nameLabel': 'Name', 'menu.categoryName': 'Category name',
    'menu.priceStar': 'Price *', 'menu.selectionType': 'Selection type',
    'menu.freePick': 'Free pick (checkbox)', 'menu.showForCats': 'Show for categories',
    'menu.priceZeroFree': 'Price (0 = free)',

    // Live Orders
    'orders.title': 'Live Orders', 'orders.noOrders': 'No orders',
    'orders.tabActive': 'Active', 'orders.tabDone': 'Done', 'orders.tabAll': 'All',
    'orders.allLocations': 'All Locations',
    'orders.selectOrder': 'Select an order', 'orders.clickToView': 'Click any order to view details',
    'orders.accept': '✓ Accept', 'orders.decline': '✕ Decline',
    'orders.markPreparing': 'Mark Preparing', 'orders.markReady': 'Mark Ready',
    'orders.markCompleted': 'Mark Completed', 'orders.declineTitle': 'Decline Order',
    'orders.eta': 'ETA (minutes)', 'orders.etaPlaceholder': 'Enter minutes',
    'orders.declineReason': 'Reason for declining…', 'orders.items': 'Items',
    'orders.total': 'Total', 'orders.payment': 'Payment', 'orders.cash': 'Cash',
    'orders.card': 'Card', 'orders.other': 'Other', 'orders.paid': 'Paid',
    'orders.unpaid': 'Unpaid', 'orders.refunded': 'Refunded',
    'orders.acceptRequiresPayment': 'Confirm payment before accepting', 'orders.method': 'Payment method',

    // Locations
    'locations.title': 'Locations', 'locations.add': 'Add Location',
    'locations.addTitle': 'Add Location', 'locations.editTitle': 'Edit Location',
    'locations.noLocations': 'No locations yet',
    'locations.name': 'Name *', 'locations.address': 'Address *',
    'locations.workingHours': 'Working Hours', 'locations.contactPhone': 'Contact Phone',
    'locations.active': 'Active', 'locations.inactive': 'Inactive',
    'locations.nameRu': 'Name (RU)', 'locations.nameKk': 'Name (KZ)',

    // Analytics
    'analytics.title': 'Analytics', 'analytics.exportCsv': 'Export CSV',
    'analytics.overview': 'Coffee shop performance overview',
    'analytics.totalRevenue': 'Total Revenue', 'analytics.completedOrders': 'completed orders',
    'analytics.totalOrders': 'Total Orders', 'analytics.uniqueCustomers': 'unique customers',
    'analytics.avgOrderValue': 'Avg Order Value', 'analytics.perCompleted': 'per completed order',
    'analytics.avgPrepTime': 'Avg Prep Time', 'analytics.fromAccept': 'from accept to complete',
    'analytics.revenueOverTime': 'Revenue Over Time', 'analytics.completedRev': 'Completed order revenue',
    'analytics.topSelling': 'Top Selling Items', 'analytics.byQuantity': 'By quantity sold',
    'analytics.revenueByLocation': 'Revenue by Location', 'analytics.perLocation': 'Completed orders per location',
    'analytics.peakHours': 'Peak Hours', 'analytics.hourlyDist': 'Order distribution by hour of day',
    'analytics.paymentMethods': 'Payment Methods', 'analytics.howPay': 'How customers pay',
    'analytics.statusBreakdown': 'Order Status Breakdown', 'analytics.allOrders': 'All orders in this period',
    'analytics.noData': 'No data for this period',
    'analytics.period': 'Period', 'analytics.today': 'Today',
    'analytics.week': 'Week', 'analytics.month': 'Month',

    // Promotions
    'promotions.title': 'Promotions', 'promotions.add': 'Add Promotion',
    'promotions.subtitle': 'Pop-up ads shown to customers on app open',
    'promotions.newPromotion': '+ New Promotion',
    'promotions.addTitle': 'New Promotion', 'promotions.editTitle': 'Edit Promotion',
    'promotions.noPromotions': 'No promotions yet', 'promotions.title_label': 'Title',
    'promotions.expiresAt': 'Expires At', 'promotions.detailsPlaceholder': 'Promotion details shown to customers…',
    'promotions.active': 'Active', 'promotions.expired': 'Expired', 'promotions.inactive': 'Inactive',
    'promotions.expires': 'Expires:', 'promotions.imageUrlOpt': 'Image URL (optional)',
    'promotions.expiresOpt': 'Expires at (optional)', 'promotions.saving': 'Saving…',
    'promotions.createFirst': 'Create your first promo to show customers a pop-up when they open the app.',

    // Customers
    'customers.title': 'Customers', 'customers.noCustomers': 'No customers yet',
    'customers.subtitle': 'Registered customer accounts',
    'customers.search': 'Search customers…', 'customers.searchPhone': 'Search by phone number…',
    'customers.noFound': 'No customers found.',
    'customers.totalOrders': 'Total Orders', 'customers.totalSpent': 'Total Spent',
    'customers.memberSince': 'Member Since', 'customers.status': 'Status',
    'customers.active': 'Active', 'customers.inactive': 'Inactive',
    'customers.phone': 'Phone', 'customers.name': 'Name',
    'customers.registered': 'Registered', 'customers.orders': 'Orders', 'customers.spent': 'Total Spent',
    'customers.backTo': 'Back to Customers', 'customers.notFound': 'Customer not found.',
    'customers.deactivated': 'Deactivated', 'customers.recentOrders': 'Recent Orders',
    'customers.noOrders': 'No orders yet.', 'customers.date': 'Date',
    'customers.location': 'Location', 'customers.items': 'Items', 'customers.total': 'Total',

    // Workers
    'workers.title': 'Workers', 'workers.add': 'Add Worker',
    'workers.newWorker': '+ New Worker', 'workers.manageStaff': 'Manage staff access',
    'workers.addTitle': 'Add Worker', 'workers.editTitle': 'Edit Worker',
    'workers.noWorkers': 'No workers yet', 'workers.noFound': 'No worker accounts found.',
    'workers.username': 'Username', 'workers.password': 'Password', 'workers.role': 'Role',
    'workers.statusCol': 'Status', 'workers.created': 'Created', 'workers.actions': 'Actions',
    'workers.resetPw': 'Reset PW', 'workers.active': 'Active', 'workers.inactive': 'Inactive',
    'workers.newAccount': 'New Worker Account', 'workers.editWorker': 'Edit Worker',
    'workers.resetTitle': 'Reset Password',
    'workers.pwMinChars': 'Password (min 6 chars)', 'workers.newPw': 'New password (min 6 chars)',
    'workers.creating': 'Creating…', 'workers.saving': 'Saving…', 'workers.resetting': 'Resetting…',

    // Order statuses
    'order.status.Created': 'Order Placed', 'order.status.Accepted': 'Accepted',
    'order.status.Preparing': 'Preparing', 'order.status.Ready': 'Ready for Pickup',
    'order.status.Completed': 'Completed', 'order.status.Declined': 'Declined',

    // Dashboard
    'dashboard.title': 'Dashboard', 'dashboard.todayRevenue': "Today's Revenue",
    'dashboard.todayOrders': "Today's Orders", 'dashboard.activeOrders': 'Active Orders',
    'dashboard.totalCustomers': 'Total Customers', 'dashboard.overview': "Today's overview",
    'dashboard.ordersToday': 'Orders Today', 'dashboard.revenueToday': 'Revenue Today',
    'dashboard.avgOrderValue': 'Avg Order Value', 'dashboard.pendingOrders': 'Pending Orders',
    'dashboard.ordersHourly': 'Orders by Hour (Today)', 'dashboard.viewPending': 'View Pending Orders',
    'dashboard.goMenu': 'Go to Menu', 'dashboard.refresh': 'Refresh', 'dashboard.retry': 'Retry',
    'dashboard.failedLoad': 'Failed to load dashboard', 'dashboard.noData': 'No order data for today yet',
  },

  ru: {
    // Navigation
    'nav.dashboard': 'Дашборд', 'nav.orders': 'Заказы (Live)', 'nav.menu': 'Управление меню',
    'nav.locations': 'Локации', 'nav.analytics': 'Аналитика', 'nav.promotions': 'Акции',
    'nav.customers': 'Клиенты', 'nav.workers': 'Сотрудники', 'nav.auditLog': 'Журнал аудита',
    'nav.signOut': 'Выйти', 'nav.console': 'Консоль управления',
    'auditLog.title': 'Журнал аудита', 'auditLog.timestamp': 'Время', 'auditLog.admin': 'Администратор',
    'auditLog.action': 'Действие', 'auditLog.entity': 'Объект', 'auditLog.details': 'Детали',
    'auditLog.noRecords': 'Записей не найдено.', 'auditLog.loadMore': 'Загрузить ещё',
    'auditLog.filterEntity': 'Фильтр по типу объекта…', 'auditLog.all': 'Все',

    // Common actions
    'action.add': 'Добавить', 'action.edit': 'Изменить', 'action.delete': 'Удалить',
    'action.save': 'Сохранить', 'action.cancel': 'Отмена', 'action.close': 'Закрыть',
    'action.confirm': 'Подтвердить', 'action.search': 'Поиск…',
    'action.refresh': 'Обновить', 'action.retry': 'Повторить',

    // Common labels
    'label.name': 'Название', 'label.description': 'Описание', 'label.price': 'Цена',
    'label.category': 'Категория', 'label.available': 'Доступно', 'label.active': 'Активно',
    'label.imageUrl': 'URL изображения', 'label.yes': 'Да', 'label.no': 'Нет',
    'label.free': 'Бесплатно', 'label.loading': 'Загрузка…', 'label.group': 'Группа',
    'label.categories': 'Категории', 'label.phone': 'Телефон', 'label.address': 'Адрес',
    'label.workingHours': 'Часы работы', 'label.saving': 'Сохранение…', 'label.inactive': 'Неактивно',
    'label.status': 'Статус', 'label.role': 'Роль', 'label.created': 'Создан', 'label.actions': 'Действия',

    // Menu management
    'menu.title': 'Управление меню', 'menu.categories': 'Категории', 'menu.allItems': 'Все блюда',
    'menu.addCategory': '+ Добавить категорию', 'menu.items': 'Блюда', 'menu.toppings': 'Топпинги',
    'menu.addItem': '+ Добавить блюдо', 'menu.addTopping': '+ Добавить топпинг',
    'menu.addMenuItemTitle': 'Добавить блюдо', 'menu.editMenuItemTitle': 'Изменить блюдо',
    'menu.addCategoryTitle': 'Добавить категорию', 'menu.editCategoryTitle': 'Изменить категорию',
    'menu.addToppingTitle': 'Добавить топпинг', 'menu.editToppingTitle': 'Изменить топпинг',
    'menu.noItems': 'Нет блюд в этой категории', 'menu.noToppings': 'Нет топпингов',
    'menu.nameLabel': 'Название', 'menu.categoryName': 'Название категории',
    'menu.priceStar': 'Цена *', 'menu.selectionType': 'Тип выбора',
    'menu.freePick': 'Свободный выбор (checkbox)', 'menu.showForCats': 'Показывать для категорий',
    'menu.priceZeroFree': 'Цена (0 = бесплатно)',

    // Live Orders
    'orders.title': 'Заказы (Live)', 'orders.noOrders': 'Нет заказов',
    'orders.tabActive': 'Активные', 'orders.tabDone': 'Завершённые', 'orders.tabAll': 'Все',
    'orders.allLocations': 'Все локации',
    'orders.selectOrder': 'Выберите заказ', 'orders.clickToView': 'Нажмите на заказ для просмотра',
    'orders.accept': '✓ Принять', 'orders.decline': '✕ Отклонить',
    'orders.markPreparing': 'Начать готовку', 'orders.markReady': 'Пометить готовым',
    'orders.markCompleted': 'Завершить заказ', 'orders.declineTitle': 'Отклонить заказ',
    'orders.eta': 'Время (мин)', 'orders.etaPlaceholder': 'Введите минуты',
    'orders.declineReason': 'Причина отклонения…', 'orders.items': 'Позиции',
    'orders.total': 'Итого', 'orders.payment': 'Оплата', 'orders.cash': 'Наличные',
    'orders.card': 'Карта', 'orders.other': 'Другое', 'orders.paid': 'Оплачено',
    'orders.unpaid': 'Не оплачено', 'orders.refunded': 'Возврат',
    'orders.acceptRequiresPayment': 'Подтвердите оплату перед принятием', 'orders.method': 'Метод оплаты',

    // Locations
    'locations.title': 'Локации', 'locations.add': 'Добавить локацию',
    'locations.addTitle': 'Добавить локацию', 'locations.editTitle': 'Изменить локацию',
    'locations.noLocations': 'Нет локаций',
    'locations.name': 'Название *', 'locations.address': 'Адрес *',
    'locations.workingHours': 'Часы работы', 'locations.contactPhone': 'Контактный телефон',
    'locations.active': 'Активна', 'locations.inactive': 'Неактивна',
    'locations.nameRu': 'Название (RU)', 'locations.nameKk': 'Название (KZ)',

    // Analytics
    'analytics.title': 'Аналитика', 'analytics.exportCsv': 'Экспорт CSV',
    'analytics.overview': 'Обзор показателей кофейни',
    'analytics.totalRevenue': 'Общая выручка', 'analytics.completedOrders': 'завершённых заказов',
    'analytics.totalOrders': 'Всего заказов', 'analytics.uniqueCustomers': 'уникальных клиентов',
    'analytics.avgOrderValue': 'Средний чек', 'analytics.perCompleted': 'за завершённый заказ',
    'analytics.avgPrepTime': 'Среднее время', 'analytics.fromAccept': 'от принятия до выполнения',
    'analytics.revenueOverTime': 'Выручка по времени', 'analytics.completedRev': 'Выручка завершённых заказов',
    'analytics.topSelling': 'Самые популярные блюда', 'analytics.byQuantity': 'По количеству продаж',
    'analytics.revenueByLocation': 'Выручка по локациям', 'analytics.perLocation': 'Завершённых заказов по локации',
    'analytics.peakHours': 'Пиковые часы', 'analytics.hourlyDist': 'Распределение заказов по часам',
    'analytics.paymentMethods': 'Способы оплаты', 'analytics.howPay': 'Как клиенты платят',
    'analytics.statusBreakdown': 'Статусы заказов', 'analytics.allOrders': 'Все заказы за период',
    'analytics.noData': 'Нет данных за период',
    'analytics.period': 'Период', 'analytics.today': 'Сегодня',
    'analytics.week': 'Эта неделя', 'analytics.month': 'Этот месяц',

    // Promotions
    'promotions.title': 'Акции', 'promotions.add': 'Добавить акцию',
    'promotions.subtitle': 'Поп-ап реклама при открытии приложения',
    'promotions.newPromotion': '+ Новая акция',
    'promotions.addTitle': 'Новая акция', 'promotions.editTitle': 'Изменить акцию',
    'promotions.noPromotions': 'Нет акций', 'promotions.title_label': 'Заголовок',
    'promotions.expiresAt': 'Истекает', 'promotions.detailsPlaceholder': 'Описание акции для клиентов…',
    'promotions.active': 'Активная', 'promotions.expired': 'Истекла', 'promotions.inactive': 'Неактивна',
    'promotions.expires': 'Истекает:', 'promotions.imageUrlOpt': 'URL изображения (необязательно)',
    'promotions.expiresOpt': 'Истекает (необязательно)', 'promotions.saving': 'Сохранение…',
    'promotions.createFirst': 'Создайте первую акцию, чтобы показывать клиентам поп-ап при открытии приложения.',

    // Customers
    'customers.title': 'Клиенты', 'customers.noCustomers': 'Нет клиентов',
    'customers.subtitle': 'Зарегистрированные аккаунты',
    'customers.search': 'Поиск клиентов…', 'customers.searchPhone': 'Поиск по номеру телефона…',
    'customers.noFound': 'Клиенты не найдены.',
    'customers.totalOrders': 'Всего заказов', 'customers.totalSpent': 'Всего потрачено',
    'customers.memberSince': 'Участник с', 'customers.status': 'Статус',
    'customers.active': 'Активен', 'customers.inactive': 'Неактивен',
    'customers.phone': 'Телефон', 'customers.name': 'Имя',
    'customers.registered': 'Зарегистрирован', 'customers.orders': 'Заказы', 'customers.spent': 'Потрачено',
    'customers.backTo': 'К клиентам', 'customers.notFound': 'Клиент не найден.',
    'customers.deactivated': 'Деактивирован', 'customers.recentOrders': 'Последние заказы',
    'customers.noOrders': 'Заказов пока нет.', 'customers.date': 'Дата',
    'customers.location': 'Локация', 'customers.items': 'Позиции', 'customers.total': 'Итого',

    // Workers
    'workers.title': 'Сотрудники', 'workers.add': 'Добавить сотрудника',
    'workers.newWorker': '+ Новый сотрудник', 'workers.manageStaff': 'Управление доступом персонала',
    'workers.addTitle': 'Добавить сотрудника', 'workers.editTitle': 'Изменить сотрудника',
    'workers.noWorkers': 'Нет сотрудников', 'workers.noFound': 'Сотрудников не найдено.',
    'workers.username': 'Логин', 'workers.password': 'Пароль', 'workers.role': 'Роль',
    'workers.statusCol': 'Статус', 'workers.created': 'Создан', 'workers.actions': 'Действия',
    'workers.resetPw': 'Сброс пароля', 'workers.active': 'Активен', 'workers.inactive': 'Неактивен',
    'workers.newAccount': 'Новый сотрудник', 'workers.editWorker': 'Изменить сотрудника',
    'workers.resetTitle': 'Сброс пароля',
    'workers.pwMinChars': 'Пароль (мин. 6 символов)', 'workers.newPw': 'Новый пароль (мин. 6 символов)',
    'workers.creating': 'Создание…', 'workers.saving': 'Сохранение…', 'workers.resetting': 'Сброс…',

    // Order statuses
    'order.status.Created': 'Заказ оформлен', 'order.status.Accepted': 'Принят',
    'order.status.Preparing': 'Готовится', 'order.status.Ready': 'Готов к выдаче',
    'order.status.Completed': 'Завершён', 'order.status.Declined': 'Отклонён',

    // Dashboard
    'dashboard.title': 'Дашборд', 'dashboard.todayRevenue': 'Выручка сегодня',
    'dashboard.todayOrders': 'Заказов сегодня', 'dashboard.activeOrders': 'Активных заказов',
    'dashboard.totalCustomers': 'Всего клиентов', 'dashboard.overview': 'Обзор за сегодня',
    'dashboard.ordersToday': 'Заказов сегодня', 'dashboard.revenueToday': 'Выручка сегодня',
    'dashboard.avgOrderValue': 'Средний чек', 'dashboard.pendingOrders': 'Ожидающих заказов',
    'dashboard.ordersHourly': 'Заказы по часам (сегодня)', 'dashboard.viewPending': 'Ожидающие заказы',
    'dashboard.goMenu': 'Перейти в меню', 'dashboard.refresh': 'Обновить', 'dashboard.retry': 'Повторить',
    'dashboard.failedLoad': 'Не удалось загрузить дашборд', 'dashboard.noData': 'Нет данных за сегодня',
  },

  kk: {
    // Navigation
    'nav.dashboard': 'Дашборд', 'nav.orders': 'Тапсырыстар (Live)', 'nav.menu': 'Мәзір басқару',
    'nav.locations': 'Мекендер', 'nav.analytics': 'Аналитика', 'nav.promotions': 'Акциялар',
    'nav.customers': 'Клиенттер', 'nav.workers': 'Қызметкерлер', 'nav.auditLog': 'Аудит журналы',
    'nav.signOut': 'Шығу', 'nav.console': 'Басқару консолі',
    'auditLog.title': 'Аудит журналы', 'auditLog.timestamp': 'Уақыт', 'auditLog.admin': 'Әкімші',
    'auditLog.action': 'Әрекет', 'auditLog.entity': 'Нысан', 'auditLog.details': 'Мәліметтер',
    'auditLog.noRecords': 'Жазбалар табылмады.', 'auditLog.loadMore': 'Тағы жүктеу',
    'auditLog.filterEntity': 'Нысан түрі бойынша сүзгі…', 'auditLog.all': 'Барлығы',

    // Common actions
    'action.add': 'Қосу', 'action.edit': 'Өзгерту', 'action.delete': 'Жою',
    'action.save': 'Сақтау', 'action.cancel': 'Болдырмау', 'action.close': 'Жабу',
    'action.confirm': 'Растау', 'action.search': 'Іздеу…',
    'action.refresh': 'Жаңарту', 'action.retry': 'Қайталау',

    // Common labels
    'label.name': 'Атауы', 'label.description': 'Сипаттама', 'label.price': 'Баға',
    'label.category': 'Санат', 'label.available': 'Қолжетімді', 'label.active': 'Белсенді',
    'label.imageUrl': 'Сурет URL', 'label.yes': 'Иә', 'label.no': 'Жоқ',
    'label.free': 'Тегін', 'label.loading': 'Жүктелуде…', 'label.group': 'Топ',
    'label.categories': 'Санаттар', 'label.phone': 'Телефон', 'label.address': 'Мекенжай',
    'label.workingHours': 'Жұмыс уақыты', 'label.saving': 'Сақталуда…', 'label.inactive': 'Белсенді емес',
    'label.status': 'Мәртебе', 'label.role': 'Рөл', 'label.created': 'Жасалды', 'label.actions': 'Іс-әрекеттер',

    // Menu management
    'menu.title': 'Мәзір басқару', 'menu.categories': 'Санаттар', 'menu.allItems': 'Барлық тауарлар',
    'menu.addCategory': '+ Санат қосу', 'menu.items': 'Тауарлар', 'menu.toppings': 'Топпингтер',
    'menu.addItem': '+ Тауар қосу', 'menu.addTopping': '+ Топпинг қосу',
    'menu.addMenuItemTitle': 'Тауар қосу', 'menu.editMenuItemTitle': 'Тауарды өзгерту',
    'menu.addCategoryTitle': 'Санат қосу', 'menu.editCategoryTitle': 'Санатты өзгерту',
    'menu.addToppingTitle': 'Топпинг қосу', 'menu.editToppingTitle': 'Топпингті өзгерту',
    'menu.noItems': 'Бұл санатта тауар жоқ', 'menu.noToppings': 'Топпингтер жоқ',
    'menu.nameLabel': 'Атауы', 'menu.categoryName': 'Санат атауы',
    'menu.priceStar': 'Баға *', 'menu.selectionType': 'Таңдау түрі',
    'menu.freePick': 'Еркін таңдау (checkbox)', 'menu.showForCats': 'Санаттар үшін көрсету',
    'menu.priceZeroFree': 'Баға (0 = тегін)',

    // Live Orders
    'orders.title': 'Тапсырыстар (Live)', 'orders.noOrders': 'Тапсырыстар жоқ',
    'orders.tabActive': 'Белсенді', 'orders.tabDone': 'Аяқталған', 'orders.tabAll': 'Барлық',
    'orders.allLocations': 'Барлық мекендер',
    'orders.selectOrder': 'Тапсырыс таңдаңыз', 'orders.clickToView': 'Мәліметтерді көру үшін тапсырысты нұқыңыз',
    'orders.accept': '✓ Қабылдау', 'orders.decline': '✕ Қабылдамау',
    'orders.markPreparing': 'Дайындауды бастау', 'orders.markReady': 'Дайын деп белгілеу',
    'orders.markCompleted': 'Тапсырысты аяқтау', 'orders.declineTitle': 'Тапсырысты қабылдамау',
    'orders.eta': 'Уақыт (мин)', 'orders.etaPlaceholder': 'Минут енгізіңіз',
    'orders.declineReason': 'Қабылдамау себебі…', 'orders.items': 'Позициялар',
    'orders.total': 'Жиыны', 'orders.payment': 'Төлем', 'orders.cash': 'Қолма-қол',
    'orders.card': 'Карта', 'orders.other': 'Басқа', 'orders.paid': 'Төленді',
    'orders.unpaid': 'Төленбеді', 'orders.refunded': 'Қайтарылды',
    'orders.acceptRequiresPayment': 'Қабылдамас бұрын төлемді растаңыз', 'orders.method': 'Төлем әдісі',

    // Locations
    'locations.title': 'Мекендер', 'locations.add': 'Мекен қосу',
    'locations.addTitle': 'Мекен қосу', 'locations.editTitle': 'Мекенді өзгерту',
    'locations.noLocations': 'Мекендер жоқ',
    'locations.name': 'Атауы *', 'locations.address': 'Мекенжай *',
    'locations.workingHours': 'Жұмыс уақыты', 'locations.contactPhone': 'Байланыс телефоны',
    'locations.active': 'Белсенді', 'locations.inactive': 'Белсенді емес',
    'locations.nameRu': 'Атауы (RU)', 'locations.nameKk': 'Атауы (KZ)',

    // Analytics
    'analytics.title': 'Аналитика', 'analytics.exportCsv': 'CSV экспорт',
    'analytics.overview': 'Кофеханның жұмыс көрсеткіштері',
    'analytics.totalRevenue': 'Жалпы табыс', 'analytics.completedOrders': 'аяқталған тапсырыс',
    'analytics.totalOrders': 'Барлық тапсырыстар', 'analytics.uniqueCustomers': 'бірегей клиенттер',
    'analytics.avgOrderValue': 'Орташа чек', 'analytics.perCompleted': 'аяқталған тапсырыс үшін',
    'analytics.avgPrepTime': 'Орташа уақыт', 'analytics.fromAccept': 'қабылдаудан аяқтауға дейін',
    'analytics.revenueOverTime': 'Уақыт бойынша табыс', 'analytics.completedRev': 'Аяқталған тапсырыс табысы',
    'analytics.topSelling': 'Ең танымал тауарлар', 'analytics.byQuantity': 'Сату санына қарай',
    'analytics.revenueByLocation': 'Мекен бойынша табыс', 'analytics.perLocation': 'Мекен бойынша тапсырыстар',
    'analytics.peakHours': 'Шыңды сағаттар', 'analytics.hourlyDist': 'Тапсырыстардың сағаттық бөлінісі',
    'analytics.paymentMethods': 'Төлем тәсілдері', 'analytics.howPay': 'Клиенттер қалай төлейді',
    'analytics.statusBreakdown': 'Тапсырыс күйлері', 'analytics.allOrders': 'Кезеңдегі барлық тапсырыстар',
    'analytics.noData': 'Кезең деректері жоқ',
    'analytics.period': 'Кезең', 'analytics.today': 'Бүгін',
    'analytics.week': 'Осы апта', 'analytics.month': 'Осы ай',

    // Promotions
    'promotions.title': 'Акциялар', 'promotions.add': 'Акция қосу',
    'promotions.subtitle': 'Қолданба ашылғанда поп-ап жарнама',
    'promotions.newPromotion': '+ Жаңа акция',
    'promotions.addTitle': 'Жаңа акция', 'promotions.editTitle': 'Акцияны өзгерту',
    'promotions.noPromotions': 'Акциялар жоқ', 'promotions.title_label': 'Тақырып',
    'promotions.expiresAt': 'Мерзімі', 'promotions.detailsPlaceholder': 'Клиенттерге арналған сипаттама…',
    'promotions.active': 'Белсенді', 'promotions.expired': 'Мерзімі өтті', 'promotions.inactive': 'Белсенді емес',
    'promotions.expires': 'Мерзімі:', 'promotions.imageUrlOpt': 'Сурет URL (міндетті емес)',
    'promotions.expiresOpt': 'Мерзімі (міндетті емес)', 'promotions.saving': 'Сақталуда…',
    'promotions.createFirst': 'Клиенттерге поп-ап көрсету үшін алғашқы акцияны жасаңыз.',

    // Customers
    'customers.title': 'Клиенттер', 'customers.noCustomers': 'Клиенттер жоқ',
    'customers.subtitle': 'Тіркелген аккаунттар',
    'customers.search': 'Клиенттерді іздеу…', 'customers.searchPhone': 'Телефон нөмірі бойынша іздеу…',
    'customers.noFound': 'Клиенттер табылмады.',
    'customers.totalOrders': 'Барлық тапсырыстар', 'customers.totalSpent': 'Жалпы жұмсалған',
    'customers.memberSince': 'Мүше болды', 'customers.status': 'Күй',
    'customers.active': 'Белсенді', 'customers.inactive': 'Белсенді емес',
    'customers.phone': 'Телефон', 'customers.name': 'Аты',
    'customers.registered': 'Тіркелген', 'customers.orders': 'Тапсырыстар', 'customers.spent': 'Жұмсалды',
    'customers.backTo': 'Клиенттерге', 'customers.notFound': 'Клиент табылмады.',
    'customers.deactivated': 'Өшірілген', 'customers.recentOrders': 'Соңғы тапсырыстар',
    'customers.noOrders': 'Тапсырыстар жоқ.', 'customers.date': 'Күн',
    'customers.location': 'Мекен', 'customers.items': 'Позициялар', 'customers.total': 'Жиыны',

    // Workers
    'workers.title': 'Қызметкерлер', 'workers.add': 'Қызметкер қосу',
    'workers.newWorker': '+ Жаңа қызметкер', 'workers.manageStaff': 'Персонал қолжетімділігін басқару',
    'workers.addTitle': 'Қызметкер қосу', 'workers.editTitle': 'Қызметкерді өзгерту',
    'workers.noWorkers': 'Қызметкерлер жоқ', 'workers.noFound': 'Қызметкерлер табылмады.',
    'workers.username': 'Логин', 'workers.password': 'Құпия сөз', 'workers.role': 'Рөл',
    'workers.statusCol': 'Мәртебе', 'workers.created': 'Жасалды', 'workers.actions': 'Іс-әрекеттер',
    'workers.resetPw': 'Пароль сфыіру', 'workers.active': 'Белсенді', 'workers.inactive': 'Белсенді емес',
    'workers.newAccount': 'Жаңа қызметкер', 'workers.editWorker': 'Қызметкерді өзгерту',
    'workers.resetTitle': 'Пароль сфыіру',
    'workers.pwMinChars': 'Пароль (мин. 6 таңба)', 'workers.newPw': 'Жаңа пароль (мин. 6 таңба)',
    'workers.creating': 'Жасалуда…', 'workers.saving': 'Сақталуда…', 'workers.resetting': 'Сфырылуда…',

    // Order statuses
    'order.status.Created': 'Тапсырыс берілді', 'order.status.Accepted': 'Қабылданды',
    'order.status.Preparing': 'Дайындалуда', 'order.status.Ready': 'Алуға дайын',
    'order.status.Completed': 'Аяқталды', 'order.status.Declined': 'Қабылданбады',

    // Dashboard
    'dashboard.title': 'Дашборд', 'dashboard.todayRevenue': 'Бүгінгі табыс',
    'dashboard.todayOrders': 'Бүгінгі тапсырыстар', 'dashboard.activeOrders': 'Белсенді тапсырыстар',
    'dashboard.totalCustomers': 'Барлық клиенттер', 'dashboard.overview': 'Бүгінгі шолу',
    'dashboard.ordersToday': 'Бүгінгі тапсырыстар', 'dashboard.revenueToday': 'Бүгінгі табыс',
    'dashboard.avgOrderValue': 'Орташа чек', 'dashboard.pendingOrders': 'Күтудегі тапсырыстар',
    'dashboard.ordersHourly': 'Сағаттық тапсырыстар (бүгін)', 'dashboard.viewPending': 'Күтудегі тапсырыстар',
    'dashboard.goMenu': 'Мәзірге өту', 'dashboard.refresh': 'Жаңарту', 'dashboard.retry': 'Қайталау',
    'dashboard.failedLoad': 'Дашборд жүктелмеді', 'dashboard.noData': 'Бүгін деректер жоқ',
  },
};

@Injectable({ providedIn: 'root' })
export class AdminLangService {
  private readonly KEY = 'yurt_admin_lang';
  readonly lang = signal<AdminLang>((localStorage.getItem(this.KEY) as AdminLang) || 'ru');

  setLang(lang: string): void {
    this.lang.set(lang as AdminLang);
    localStorage.setItem(this.KEY, lang);
  }

  t(key: string): string {
    return T[this.lang()][key] ?? key;
  }
}
